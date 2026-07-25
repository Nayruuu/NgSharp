using System.Text;
using System.Collections.Generic;

namespace NgSharp.Parsing;

// Desugars the text-level control-flow syntax into markers the parsers understand:
// @if (E) { B } -> <ng-container [if]="E">B</ng-container> (same for @for / @else if / @else /
// @switch / @case / @default). An @else (if) is only recognised right after the closing brace of its
// chain; an @case / @default only directly between an @switch's braces. Brace matching skips {{ }}
// and balances literal braces (CSS, etc.) so only a block's own closing brace closes it.
internal static class ControlFlowPreprocessor
{
    #region Fields

    // Text-dialect marker sentinels. The HTML dialect desugars to <ng-container> wrapper elements —
    // typable, which text mode cannot afford: an author-written literal marker would execute, and an
    // expression holding a double quote would break the [attr]="…" round-trip. Text markers are
    // therefore CONTROL-CHARACTER sequences no keyboard produces, and the expression travels VERBATIM
    // (any quoting welcome): U+0001 kind U+0002 expression (U+0002 extra)? U+0003 opens a block;
    // U+0001 U+0003 closes one. The extra slot is [for-var] / [render-context], keyed by the kind.
    internal const char TEXT_MARKER_START = '\u0001';

    internal const char TEXT_MARKER_SEPARATOR = '\u0002';

    internal const char TEXT_MARKER_END = '\u0003';

    #endregion

    #region Public methods

    // The HTML-dialect expansion — byte-identical to the historical behavior.
    public static string Expand(string template) => Expand(template, text: false);

    // The text-dialect expansion: sentinel markers (see above) and no HTML-comment skipping in the
    // @else interstice — <!-- --> is not a text concept, so it stays literal like everything else.
    public static string ExpandText(string template) => Expand(template, text: true);

    #endregion

    #region Private methods

    private static string Expand(string template, bool text)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('@') < 0)
        {
            return template;
        }

        // Validation-only: null on every plain parse. The hooks below either sit in cold branches or
        // behind this null check, and none of them changes the expansion by a byte.
        var collector = DiagnosticCollector.Current;

        var builder = new StringBuilder(template.Length + 32);
        // Block kinds: 'i' = @if / @else if (an @else may follow), 'f' = @for, 'e' = @else (terminal),
        // 's' = @switch (only @case / @default open inside it), 'c' = @case, 'd' = @default.
        // `at` is the block's '@' offset — only diagnostics read it.
        var openBlocks = new Stack<(int depth, char kind, int at)>();
        // Validation-only (null otherwise): the `at` of every @switch that grew at least one @case,
        // so a caseless @switch can be flagged when it closes.
        HashSet<int> switchesWithCase = null;
        var depth = 0;
        var i = 0;

        while (i < template.Length)
        {
            if (i + 1 < template.Length && template[i] == '{' && template[i + 1] == '{')
            {
                var end = template.IndexOf("}}", i + 2, System.StringComparison.Ordinal);
                if (end < 0)
                {
                    end = template.Length - 2;
                }

                builder.Append(template, i, (end + 2) - i);
                i = end + 2;
                continue;
            }

            if (template[i] == '@' && TryReadOpener(template, i, out var directive, out var expression, out var next))
            {
                if (directive == "for")
                {
                    collector?.CheckForOfMistake(expression, i);

                    // @for (Var of Collection) -> [for]="Collection" [for-var]="Var"; @for (Collection) -> [for]="Collection".
                    SplitFor(expression, out var collection, out var loopVar);
                    AppendOpenMarker(builder, text, "for", collection, "for-var", loopVar);
                }
                else
                {
                    AppendOpenMarker(builder, text, directive, expression);
                }

                depth++;
                openBlocks.Push((depth, directive == "for" ? 'f' : directive == "switch" ? 's' : 'i', i));
                i = next;
                collector?.AddCheckpoint(builder.Length, i);
                continue;
            }

            // @case (E) { / @default { — only legal DIRECTLY between an @switch's braces; anywhere
            // else the keyword stays literal text and validation flags the orphan.
            if (template[i] == '@' && TryReadCaseOrDefault(template, i, out var caseDirective, out var caseExpression, out var caseNext))
            {
                if (openBlocks.Count > 0 && openBlocks.Peek().kind == 's' && openBlocks.Peek().depth == depth)
                {
                    if (collector is not null && caseDirective == "case")
                    {
                        (switchesWithCase ??= new HashSet<int>()).Add(openBlocks.Peek().at);
                    }

                    AppendOpenMarker(builder, text, caseDirective, caseExpression ?? string.Empty);
                    depth++;
                    openBlocks.Push((depth, caseDirective == "case" ? 'c' : 'd', i));
                    i = caseNext;
                    collector?.AddCheckpoint(builder.Length, i);
                    continue;
                }

                collector?.Report(DiagnosticSeverity.Error,
                    $"Orphan '@{caseDirective}' — it is only legal directly between the braces of an @switch block; as written it renders as literal text.", i);
            }

            // @render(name[, ctx]) — a leaf (no { body }): a self-closing marker for RenderTemplateNode.
            if (template[i] == '@' && TryReadRender(template, i, out var renderName, out var renderContext, out var renderNext))
            {
                AppendOpenMarker(builder, text, "render", renderName, "render-context", renderContext);
                AppendCloseMarker(builder, text);
                i = renderNext;
                collector?.AddCheckpoint(builder.Length, i);
                continue;
            }

            if (template[i] == '{')
            {
                depth++;
                builder.Append('{');
                i++;
                continue;
            }

            if (template[i] == '}')
            {
                if (openBlocks.Count > 0 && openBlocks.Peek().depth == depth)
                {
                    var block = openBlocks.Pop();
                    AppendCloseMarker(builder, text);
                    if (depth > 0)
                    {
                        depth--;
                    }

                    if (block.kind == 's' && collector is not null
                        && (switchesWithCase is null || switchesWithCase.Contains(block.at) == false))
                    {
                        collector.Report(DiagnosticSeverity.Warning,
                            "'@switch' without any '@case' — the block can only render its '@default' (or nothing).", block.at);
                    }

                    if (block.kind == 'i' && TryReadElse(template, i + 1, text, out var elseDirective, out var elseExpression, out var elseNext, out var elseAt))
                    {
                        AppendOpenMarker(builder, text, elseDirective, elseExpression ?? string.Empty);
                        depth++;
                        openBlocks.Push((depth, elseDirective == "else-if" ? 'i' : 'e', elseAt));
                        i = elseNext;
                        collector?.AddCheckpoint(builder.Length, i);
                        continue;
                    }

                    i++;
                    collector?.AddCheckpoint(builder.Length, i);
                    continue;
                }

                builder.Append('}');
                if (depth > 0)
                {
                    depth--;
                }

                i++;
                continue;
            }

            // An @else reaching this fall-through was NOT consumed after an @if chain's '}' — it stays
            // literal text at render. Only shape-checked (TryReadElse), so 'user@else.com' stays quiet.
            if (template[i] == '@' && collector is not null
                && TryReadElse(template, i, text, out _, out _, out _, out _))
            {
                collector.Report(DiagnosticSeverity.Error,
                    "Orphan '@else' — it must directly follow the closing '}' of an @if / @else if block; as written it renders as literal text.", i);
            }

            builder.Append(template[i]);
            i++;
        }

        if (collector is not null && openBlocks.Count > 0)
        {
            foreach (var block in openBlocks)
            {
                collector.Report(DiagnosticSeverity.Error, block.kind switch
                {
                    'f' => "Unclosed '@for' block — its closing '}' is missing, so the block swallows the rest of the template.",
                    'e' => "Unclosed '@else' block — its closing '}' is missing, so the block swallows the rest of the template.",
                    's' => "Unclosed '@switch' block — its closing '}' is missing, so the block swallows the rest of the template.",
                    'c' => "Unclosed '@case' block — its closing '}' is missing, so the block swallows the rest of the template.",
                    'd' => "Unclosed '@default' block — its closing '}' is missing, so the block swallows the rest of the template.",
                    _ => "Unclosed '@if' block — its closing '}' is missing, so the block swallows the rest of the template.",
                }, block.at);
            }
        }

        return builder.ToString();
    }

    // One open marker, in the dialect's shape. HTML: <ng-container [kind]="expression"( [extraName]=
    // "extra")?> — byte-identical to the historical emission. Text: the sentinel sequence, expression
    // and extra VERBATIM (the extra name is implied by the kind).
    private static void AppendOpenMarker(StringBuilder builder, bool text, string kind, string expression, string extraName = null, string extra = null)
    {
        if (text)
        {
            builder.Append(TEXT_MARKER_START).Append(kind).Append(TEXT_MARKER_SEPARATOR).Append(expression);
            if (extra is not null)
            {
                builder.Append(TEXT_MARKER_SEPARATOR).Append(extra);
            }

            builder.Append(TEXT_MARKER_END);

            return;
        }

        builder.Append("<ng-container [").Append(kind).Append("]=\"").Append(expression).Append('"');
        if (extra is not null)
        {
            builder.Append(" [").Append(extraName).Append("]=\"").Append(extra).Append('"');
        }

        builder.Append('>');
    }

    private static void AppendCloseMarker(StringBuilder builder, bool text)
    {
        if (text)
        {
            builder.Append(TEXT_MARKER_START).Append(TEXT_MARKER_END);

            return;
        }

        builder.Append("</ng-container>");
    }

    private static bool TryReadOpener(string source, int i, out string directive, out string expression, out int next)
    {
        directive = null;
        expression = null;
        next = i;

        var j = i + 1;
        if (MatchesKeyword(source, j, "if"))
        {
            directive = "if";
            j += 2;
        }
        else if (MatchesKeyword(source, j, "for"))
        {
            directive = "for";
            j += 3;
        }
        else if (MatchesKeyword(source, j, "switch"))
        {
            directive = "switch";
            j += 6;
        }
        else
        {
            return false;
        }

        j = SkipWhitespace(source, j);

        if (TryReadParens(source, ref j, out expression) == false)
        {
            return false;
        }

        j = SkipWhitespace(source, j);

        if (j >= source.Length || source[j] != '{')
        {
            return false;
        }

        next = j + 1;

        return true;
    }

    // Recognises an @switch arm opener: directive is "case" (with expression) or "default"
    // (expression null); next is the index past the arm's opening '{'. Shape-checked only — the
    // caller decides whether the position (directly inside an @switch) makes it an arm or an orphan.
    private static bool TryReadCaseOrDefault(string source, int i, out string directive, out string expression, out int next)
    {
        directive = null;
        expression = null;
        next = i;

        var j = i + 1;
        if (MatchesKeyword(source, j, "case"))
        {
            j = SkipWhitespace(source, j + 4);

            if (TryReadParens(source, ref j, out expression) == false)
            {
                return false;
            }

            j = SkipWhitespace(source, j);

            if (j >= source.Length || source[j] != '{')
            {
                return false;
            }

            directive = "case";
            next = j + 1;

            return true;
        }

        if (MatchesKeyword(source, j, "default"))
        {
            j = SkipWhitespace(source, j + 7);

            if (j < source.Length && source[j] == '{')
            {
                directive = "default";
                next = j + 1;

                return true;
            }
        }

        return false;
    }

    // name is a literal template reference; the optional context expression splits on the FIRST comma.
    private static bool TryReadRender(string source, int i, out string name, out string context, out int next)
    {
        name = null;
        context = null;
        next = i;

        var j = i + 1;
        if (MatchesKeyword(source, j, "render") == false)
        {
            return false;
        }

        j = SkipWhitespace(source, j + 6);

        if (TryReadParens(source, ref j, out var args) == false)
        {
            return false;
        }

        var comma = args.IndexOf(',');
        if (comma < 0)
        {
            name = args.Trim();
        }
        else
        {
            name = args.Substring(0, comma).Trim();
            context = args.Substring(comma + 1).Trim();
        }

        if (name.Length == 0)
        {
            return false;
        }

        next = j;

        return true;
    }

    // Recognises an @else / @else if right after a closing brace: directive is "else-if" (with
    // expression) or "else" (expression null); next is the index past the block's opening '{';
    // atPos is the '@' offset (diagnostics only).
    private static bool TryReadElse(string source, int pos, bool text, out string directive, out string expression, out int next, out int atPos)
    {
        directive = null;
        expression = null;
        next = pos;

        // HTML: whitespace/HTML comments before @else are skipped — must match the parser's else-chain
        // interstice rule. Text: whitespace ONLY — <!-- --> is plain characters there, and characters
        // between the brace and the @else break the chain like any other literal text.
        var j = text ? SkipWhitespace(source, pos) : SkipWhitespaceAndComments(source, pos);
        atPos = j;

        if (j >= source.Length || source[j] != '@')
        {
            return false;
        }

        j++;
        if (MatchesKeyword(source, j, "else") == false)
        {
            return false;
        }

        j = SkipWhitespace(source, j + 4);

        if (MatchesKeyword(source, j, "if"))
        {
            j = SkipWhitespace(source, j + 2);

            if (TryReadParens(source, ref j, out expression) == false)
            {
                return false;
            }

            j = SkipWhitespace(source, j);

            if (j >= source.Length || source[j] != '{')
            {
                return false;
            }

            directive = "else-if";
            next = j + 1;

            return true;
        }

        if (j < source.Length && source[j] == '{')
        {
            directive = "else";
            next = j + 1;

            return true;
        }

        return false;
    }

    // Reads a balanced parenthesised group into its trimmed inner text; false when unbalanced.
    private static bool TryReadParens(string source, ref int j, out string expression)
    {
        expression = null;

        if (j >= source.Length || source[j] != '(')
        {
            return false;
        }

        j++;

        var start = j;
        var parens = 1;

        while (j < source.Length && parens > 0)
        {
            if (source[j] == '(')
            {
                parens++;
            }
            else if (source[j] == ')')
            {
                parens--;
                if (parens == 0)
                {
                    break;
                }
            }

            j++;
        }

        if (parens != 0)
        {
            return false;
        }

        expression = source.Substring(start, j - start).Trim();
        j++;

        return true;
    }

    private static int SkipWhitespace(string source, int j)
    {
        while (j < source.Length && char.IsWhiteSpace(source[j]))
        {
            j++;
        }

        return j;
    }

    // Stops at an unterminated comment so the caller can decide (the raw text then flows through as-is).
    private static int SkipWhitespaceAndComments(string source, int j)
    {
        while (true)
        {
            j = SkipWhitespace(source, j);

            if (j + 3 < source.Length && source[j] == '<' && source[j + 1] == '!' && source[j + 2] == '-' && source[j + 3] == '-')
            {
                var end = source.IndexOf("-->", j + 4, System.StringComparison.Ordinal);
                if (end < 0)
                {
                    return j;
                }

                j = end + 3;
                continue;
            }

            return j;
        }
    }

    // "item of items" -> ("items", "item"); an Angular "; track ..." suffix is stripped; no leading
    // "ident of " -> (whole expression, null), i.e. the classic implicit @for (Collection).
    private static void SplitFor(string expression, out string collection, out string loopVar)
    {
        var expr = expression;
        var semi = expr.IndexOf(';');
        if (semi >= 0)
        {
            expr = expr.Substring(0, semi);
        }

        expr = expr.Trim();

        var ofIdx = expr.IndexOf(" of ", System.StringComparison.Ordinal);
        if (ofIdx > 0)
        {
            var left = expr.Substring(0, ofIdx).Trim();
            if (IsIdentifier(left))
            {
                loopVar = left;
                collection = expr.Substring(ofIdx + 4).Trim();

                return;
            }
        }

        collection = expr;
        loopVar = null;
    }

    private static bool IsIdentifier(string text)
    {
        if (text.Length == 0 || (char.IsLetter(text[0]) || text[0] == '_') == false)
        {
            return false;
        }

        for (var k = 1; k < text.Length; k++)
        {
            if ((char.IsLetterOrDigit(text[k]) || text[k] == '_') == false)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesKeyword(string source, int i, string keyword)
    {
        if (i + keyword.Length > source.Length)
        {
            return false;
        }

        for (var k = 0; k < keyword.Length; k++)
        {
            if (source[i + k] != keyword[k])
            {
                return false;
            }
        }

        var after = i + keyword.Length;

        return after >= source.Length || char.IsLetterOrDigit(source[after]) == false;
    }

    #endregion
}
