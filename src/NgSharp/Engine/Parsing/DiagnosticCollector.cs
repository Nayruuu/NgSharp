using System;
using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Parsing;

// The parse-time diagnostics collector behind HtmlBuilder.Validate. It is AMBIENT and OPT-IN:
// Current is null on every plain Compile/Build parse, and each instrumentation site in the
// preprocessor / parsers either reads it once per parse or only inside a cold failure branch — so
// the normal parse pays nothing and emits a byte-identical program whether the hooks exist or not.
// Set/cleared around a validation parse only (HtmlBuilder.Validate, single-threaded try/finally).
internal sealed class DiagnosticCollector
{
    #region Fields

    [ThreadStatic]
    private static DiagnosticCollector _current;

    // The ORIGINAL template — every published Position is an offset into this string.
    private readonly string _source;

    // Pipe names registered on the validating builder; unknown names downgrade to a Warning (a pipe
    // may legitimately be registered after Compile — the registry is read at render time).
    private readonly ICollection<string> _knownPipes;

    private readonly List<TemplateDiagnostic> _diagnostics = new List<TemplateDiagnostic>();

    // Preprocessor drift map: (offset in the EXPANDED text, offset in the ORIGINAL template) pairs,
    // appended in ascending order each time a desugared marker changes the two texts' relative
    // lengths. Empty = the expansion was the identity (no @-block syntax).
    private readonly List<(int Expanded, int Original)> _checkpoints = new List<(int Expanded, int Original)>();

    // The expression currently in the slow parse path (trimmed) — message context and the position
    // anchor for expression-level findings.
    private string _expression;

    // Set when the interpolation site already reported an empty '{{ }}' with an exact position, so
    // the generic empty-expression check right after it stays silent.
    private bool _suppressEmptyExpression;

    #endregion

    #region Properties

    // Null on every non-validation parse — the single fact the zero-cost contract rests on.
    public static DiagnosticCollector Current => _current;

    #endregion

    #region Constructors

    public DiagnosticCollector(string source, ICollection<string> knownPipes)
    {
        _source = source;
        _knownPipes = knownPipes;
    }

    #endregion

    #region Public methods

    // Activation is strictly scoped: HtmlBuilder.Validate sets, parses, and ALWAYS clears (finally).
    public static void SetCurrent(DiagnosticCollector collector) => _current = collector;

    public static void ClearCurrent() => _current = null;

    // Position already in ORIGINAL-template coordinates (preprocessor sites).
    public void Report(DiagnosticSeverity severity, string message, int position)
        => _diagnostics.Add(new TemplateDiagnostic(severity, message, Clamp(position)));

    // Position in EXPANDED-text coordinates (parser sites) — mapped back through the drift map.
    public void ReportExpanded(DiagnosticSeverity severity, string message, int expandedPosition)
        => Report(severity, message, MapToOriginal(expandedPosition));

    public void AddCheckpoint(int expandedPosition, int originalPosition)
        => _checkpoints.Add((expandedPosition, originalPosition));

    // ---- Expression-level hooks (ExpressionParser) ------------------------------------------------

    public void BeginExpression(string trimmed) => _expression = trimmed;

    // Post-parse checks the happy path never performs: empty input, unconsumed tail tokens.
    public void EndExpression(List<Token> tokens, int pos)
    {
        if (_expression is null)
        {
            return;
        }

        if (_expression.Length == 0)
        {
            if (_suppressEmptyExpression)
            {
                _suppressEmptyExpression = false;
            }
            else
            {
                Report(DiagnosticSeverity.Error, "Empty expression — the binding evaluates to nothing.", LocateExpression());
            }
        }
        else if (pos < tokens.Count && tokens[pos].Kind != TokenKind.End)
        {
            ReportInExpression(DiagnosticSeverity.Error, $"Unexpected '{tokens[pos].Text}' after a complete expression");
        }

        _expression = null;
    }

    public void ReportInExpression(DiagnosticSeverity severity, string detail)
    {
        // An EMPTY expression trips the grammar everywhere it looks for an operand — the dedicated
        // empty-expression report (EndExpression) says it once; the token-level noise stays out.
        if (string.IsNullOrEmpty(_expression))
        {
            return;
        }

        Report(severity, $"{detail} in expression '{_expression}'.", LocateExpression());
    }

    public void ReportUnexpectedToken(Token token)
        => ReportInExpression(DiagnosticSeverity.Error, token.Kind == TokenKind.End
            ? "Unexpected end of expression (an operand is missing)"
            : $"Unexpected '{token.Text}'");

    public void CheckPipeName(string name)
    {
        if (_knownPipes is not null && _knownPipes.Contains(name) == false)
        {
            Report(DiagnosticSeverity.Warning,
                $"Unknown pipe '{name}' — not registered on this builder. Register it with RegisterPipe<T>() before rendering, or the render will throw.",
                LocateExpression());
        }
    }

    // A '/' or '%' whose divisor is the LITERAL zero (0.0 included): statically decidable, unlike a
    // variable divisor — the lenient render always yields 0 there, and a strict render throws.
    public void CheckLiteralZeroDivisor(string op, Expression divisor)
    {
        if (divisor is LiteralExpression literal
            && literal.Value.ValueKind == JsonValueKind.Number
            && literal.Value.GetDouble() == 0d)
        {
            ReportInExpression(DiagnosticSeverity.Warning,
                $"{(op == "/" ? "Division" : "Modulo")} by the literal 0 — the lenient render always yields 0, and a strict render throws");
        }
    }

    public void SuppressNextEmptyExpression() => _suppressEmptyExpression = true;

    // ---- Template hooks (TemplateParser, both dialects) -------------------------------------------

    // An @if/[if]/[else-if] condition that can STATICALLY never be a boolean: under strict truthiness
    // the lenient render always skips the body (and always renders the else branch); strict throws.
    public void CheckIfCondition(Expression condition, string raw, int expandedPosition)
    {
        var what = DescribeNonBoolean(condition);
        if (what is null)
        {
            return;
        }

        // An EMPTY/unparsable expression also degrades to a Null-kind literal — that case is already
        // an "Empty expression" error; only the spelled-out 'null' keyword earns the always-false warning.
        if (what == "null" && raw.Trim() != "null")
        {
            return;
        }

        ReportExpanded(DiagnosticSeverity.Warning,
            $"Always-false condition '{raw}' — it is statically {what}, and strict truthiness never coerces: the body never renders, an else branch always does (a strict render throws instead). Use a boolean expression — a comparison, '!', '&&'/'||', or a boolean property.",
            expandedPosition);
    }

    // ---- Preprocessor hooks (ControlFlowPreprocessor) ---------------------------------------------

    // The classic Angular slip: '@for (x in Items)' parses as a single collection expression, finds
    // nothing, and the whole block silently renders zero times.
    public void CheckForOfMistake(string expression, int position)
    {
        var i = 0;
        while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_' || expression[i] == '$'))
        {
            i++;
        }

        if (i == 0 || i >= expression.Length || char.IsWhiteSpace(expression[i]) == false)
        {
            return;
        }

        var variable = expression.Substring(0, i);

        while (i < expression.Length && char.IsWhiteSpace(expression[i]))
        {
            i++;
        }

        if (i + 2 < expression.Length && expression[i] == 'i' && expression[i + 1] == 'n' && char.IsWhiteSpace(expression[i + 2]))
        {
            var collection = expression.Substring(i + 2).Trim();
            Report(DiagnosticSeverity.Error,
                $"'@for ({expression})' uses 'in' — did you mean '@for ({variable} of {collection})'? NgSharp loops use 'of'; as written the block renders nothing.",
                position);
        }
    }

    public IReadOnlyList<TemplateDiagnostic> Finish()
    {
        // Stable sort: source order for the reader; ties keep detection order.
        _diagnostics.Sort(static (a, b) => a.Position.CompareTo(b.Position));

        return _diagnostics;
    }

    #endregion

    #region Private methods

    // Best effort: an expression's diagnostics point at its first occurrence in the source (the text
    // travels VERBATIM through desugaring, so the search hits for both dialects).
    private int LocateExpression()
    {
        if (string.IsNullOrEmpty(_expression))
        {
            return 0;
        }

        var position = _source.IndexOf(_expression, StringComparison.Ordinal);

        return position < 0 ? 0 : position;
    }

    private int MapToOriginal(int expandedPosition)
    {
        // Last checkpoint at or before the position wins; before the first (or with no expansion at
        // all) the two texts are aligned 1:1.
        for (var i = _checkpoints.Count - 1; i >= 0; i--)
        {
            if (_checkpoints[i].Expanded <= expandedPosition)
            {
                return Clamp(_checkpoints[i].Original + (expandedPosition - _checkpoints[i].Expanded));
            }
        }

        return Clamp(expandedPosition);
    }

    private int Clamp(int position)
        => position < 0 ? 0 : position > _source.Length ? _source.Length : position;

    // The static shapes strict truthiness can never satisfy. A PathExpression stays unknowable (never
    // flagged); Comparison/Not/Logical are boolean by construction; a string literal that parses as a
    // boolean ('true'/'false') coerces, so it stays quiet too.
    private static string DescribeNonBoolean(Expression condition)
    {
        switch (condition)
        {
            case LiteralExpression literal when literal.Value.ValueKind == JsonValueKind.Number:
                return "a number";

            case LiteralExpression literal when literal.Value.ValueKind == JsonValueKind.Null:
                return "null";

            case LiteralExpression literal when literal.Value.ValueKind == JsonValueKind.String
                && bool.TryParse(literal.Value.GetString(), out _) == false:
                return "a string";

            case ArithmeticExpression arithmetic when IsStaticallyNumeric(arithmetic):
                return "numeric (an arithmetic result)";

            case TernaryExpression ternary when DescribeNonBoolean(ternary.WhenTrue) is not null
                && DescribeNonBoolean(ternary.WhenFalse) is not null:
                return "non-boolean (both ternary branches are)";

            default:
                return null;
        }
    }

    // '+' may concatenate when an operand is a string, so only all-numeric shapes qualify there; the
    // four other operators always produce a number, whatever their operands.
    private static bool IsStaticallyNumeric(Expression expression)
        => expression switch
        {
            LiteralExpression literal => literal.Value.ValueKind == JsonValueKind.Number,
            ArithmeticExpression arithmetic => arithmetic.Operator != "+"
                || (IsStaticallyNumeric(arithmetic.Left) && IsStaticallyNumeric(arithmetic.Right)),
            _ => false
        };

    #endregion
}
