using System.Globalization;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Parsing;

internal static class ExpressionParser
{
    #region Fields

    // The left operand used to model unary minus as (0 - operand); immutable, so shared safely.
    private static readonly LiteralExpression Zero = new LiteralExpression(NgElement.Parse("0"));

    #endregion

    #region Public methods

    public static Expression Parse(string input)
    {
        // Fast path for a bare property path ("Available", "Items[0].City") — must keep exact parity
        // with the slow path (the lexer's single-Identifier rule + ParsePrimary), null/true/false being
        // the only keyword exceptions (case-sensitive).
        var trimmed = input.Trim();
        if (IsBarePath(trimmed))
        {
            return new PathExpression(trimmed);
        }

        // Validation-only hooks: Current is null on a plain parse, so this is one dead branch per
        // slow-path expression; the returned AST is identical either way.
        var collector = DiagnosticCollector.Current;
        collector?.BeginExpression(trimmed);

        var tokens = ExpressionLexer.Tokenize(input);
        var pos = 0;

        var expression = ParseTernary(tokens, ref pos);
        collector?.EndExpression(tokens, pos);

        return expression;
    }

    #endregion

    #region Private methods

    private static bool IsBarePath(string text)
    {
        if (text.Length == 0 || (char.IsLetter(text[0]) || text[0] == '_' || text[0] == '$') == false)
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            var ch = text[i];
            if ((char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == '[' || ch == ']' || ch == '$') == false)
            {
                return false;
            }
        }

        return text != "null" && text != "true" && text != "false";
    }

    // ternary := logicalOr ('?' ternary ':' ternary)?
    private static Expression ParseTernary(List<Token> tokens, ref int pos)
    {
        var condition = ParseLogicalOr(tokens, ref pos);

        if (Peek(tokens, pos).Kind == TokenKind.Question)
        {
            pos++;
            var whenTrue = ParseTernary(tokens, ref pos);

            if (Peek(tokens, pos).Kind == TokenKind.Colon)
            {
                pos++;
            }
            else
            {
                DiagnosticCollector.Current?.ReportInExpression(DiagnosticSeverity.Error, "Missing ':' in the '?:' ternary");
            }

            var whenFalse = ParseTernary(tokens, ref pos);

            return new TernaryExpression(condition, whenTrue, whenFalse);
        }

        return condition;
    }

    // logicalOr := logicalAnd ('||' logicalAnd)*
    private static Expression ParseLogicalOr(List<Token> tokens, ref int pos)
    {
        var left = ParseLogicalAnd(tokens, ref pos);

        while (Peek(tokens, pos).Kind == TokenKind.Or)
        {
            pos++;
            var right = ParseLogicalAnd(tokens, ref pos);
            left = new LogicalExpression(left, "||", right);
        }

        return left;
    }

    // logicalAnd := pipe ('&&' pipe)*   ('&&' binds tighter than '||')
    private static Expression ParseLogicalAnd(List<Token> tokens, ref int pos)
    {
        var left = ParsePipe(tokens, ref pos);

        while (Peek(tokens, pos).Kind == TokenKind.And)
        {
            pos++;
            var right = ParsePipe(tokens, ref pos);
            left = new LogicalExpression(left, "&&", right);
        }

        return left;
    }

    // pipe := comparison ('|' IDENT (':' primary)*)*
    private static Expression ParsePipe(List<Token> tokens, ref int pos)
    {
        var source = ParseComparison(tokens, ref pos);

        while (Peek(tokens, pos).Kind == TokenKind.Pipe)
        {
            pos++;

            var name = Peek(tokens, pos).Text;
            if (Peek(tokens, pos).Kind == TokenKind.Identifier)
            {
                pos++;
                DiagnosticCollector.Current?.CheckPipeName(name);
            }
            else
            {
                DiagnosticCollector.Current?.ReportInExpression(DiagnosticSeverity.Error, "Missing pipe name after '|'");
            }

            var arguments = new List<Expression>();
            while (Peek(tokens, pos).Kind == TokenKind.Colon)
            {
                pos++;
                // ParseUnary (not ParsePrimary) so a negative argument ({{ x | pipe:-5 }}) keeps its '-'.
                arguments.Add(ParseUnary(tokens, ref pos));
            }

            source = new PipeExpression(source, name, arguments);
        }

        return source;
    }

    // comparison := additive (OP additive)?
    private static Expression ParseComparison(List<Token> tokens, ref int pos)
    {
        var left = ParseAdditive(tokens, ref pos);

        if (Peek(tokens, pos).Kind == TokenKind.Operator)
        {
            var op = tokens[pos].Text;
            pos++;

            var right = ParseAdditive(tokens, ref pos);

            return new ComparisonExpression(left, op, right);
        }

        return left;
    }

    // additive := multiplicative (('+' | '-') multiplicative)*   (left-associative)
    private static Expression ParseAdditive(List<Token> tokens, ref int pos)
    {
        var left = ParseMultiplicative(tokens, ref pos);

        while (Peek(tokens, pos).Kind == TokenKind.Arithmetic
            && (Peek(tokens, pos).Text == "+" || Peek(tokens, pos).Text == "-"))
        {
            var op = tokens[pos].Text;
            pos++;
            var right = ParseMultiplicative(tokens, ref pos);
            left = new ArithmeticExpression(left, op, right);
        }

        return left;
    }

    // multiplicative := unary (('*' | '/' | '%') unary)*   (binds tighter than +/-, left-associative)
    private static Expression ParseMultiplicative(List<Token> tokens, ref int pos)
    {
        var left = ParseUnary(tokens, ref pos);

        while (Peek(tokens, pos).Kind == TokenKind.Arithmetic
            && (Peek(tokens, pos).Text == "*" || Peek(tokens, pos).Text == "/" || Peek(tokens, pos).Text == "%"))
        {
            var op = tokens[pos].Text;
            pos++;
            var right = ParseUnary(tokens, ref pos);
            if (op != "*")
            {
                DiagnosticCollector.Current?.CheckLiteralZeroDivisor(op, right);
            }

            left = new ArithmeticExpression(left, op, right);
        }

        return left;
    }

    // unary := '!' unary | '-' unary | '+' unary | primary
    // ('!' / '-' bind tighter than the binary operators: '!a == b' is '(!a) == b'; '-a * b' is '(-a) * b')
    private static Expression ParseUnary(List<Token> tokens, ref int pos)
    {
        var token = Peek(tokens, pos);

        if (token.Kind == TokenKind.Not)
        {
            pos++;

            return new NotExpression(ParseUnary(tokens, ref pos));
        }

        if (token.Kind == TokenKind.Arithmetic && token.Text == "-")
        {
            pos++;

            return new ArithmeticExpression(Zero, "-", ParseUnary(tokens, ref pos));
        }

        if (token.Kind == TokenKind.Arithmetic && token.Text == "+")
        {
            pos++;   // Unary plus: a no-op.

            return ParseUnary(tokens, ref pos);
        }

        return ParsePrimary(tokens, ref pos);
    }

    private static Expression ParsePrimary(List<Token> tokens, ref int pos)
    {
        var token = Peek(tokens, pos);

        switch (token.Kind)
        {
            case TokenKind.Number:
                pos++;
                return new LiteralExpression(NumberLiteral(token.Text));

            case TokenKind.String:
                pos++;
                return new LiteralExpression(StringLiteral(token.Text));

            case TokenKind.Identifier:
                pos++;
                if (token.Text == "null" || token.Text == "true" || token.Text == "false")
                {
                    return new LiteralExpression(NgElement.Parse(token.Text));
                }

                return new PathExpression(token.Text) { Guarded = token.Guarded };

            case TokenKind.LParen:
                pos++;
                var inner = ParseTernary(tokens, ref pos);
                if (Peek(tokens, pos).Kind == TokenKind.RParen)
                {
                    pos++;
                }
                else
                {
                    DiagnosticCollector.Current?.ReportInExpression(DiagnosticSeverity.Error, "Missing closing ')'");
                }

                return inner;

            default:
                DiagnosticCollector.Current?.ReportUnexpectedToken(token);
                pos++;

                return new LiteralExpression(NgElement.Parse("null"));
        }
    }

    private static Token Peek(List<Token> tokens, int pos)
        => pos < tokens.Count ? tokens[pos] : tokens[tokens.Count - 1];

    // Culture-invariant numeric literal: integral -> long, fractional -> double; a non-JSON-shaped
    // number ("007", "1.", ".5", "1.2.3") degrades to a string literal. (The lexer only emits digits
    // and dots here — no sign, no exponent.)
    private static NgElement NumberLiteral(string text)
    {
        if (IsJsonNumberShape(text) == false)
        {
            return StringLiteral(text);
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return NgElement.FromParsedNumber(l);
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return NgElement.FromParsedNumber(d);
        }

        return StringLiteral(text);
    }

    // digits ('.') digits — one dot max, a digit on each side, no leading zero unless the integer part is "0".
    private static bool IsJsonNumberShape(string text)
    {
        var dot = text.IndexOf('.');
        if (dot != text.LastIndexOf('.'))
        {
            return false;
        }

        var intLen = dot < 0 ? text.Length : dot;
        if (intLen == 0 || (dot >= 0 && dot == text.Length - 1))
        {
            return false;
        }

        if (text[0] == '0' && intLen > 1)
        {
            return false;
        }

        return true;
    }

    private static NgElement StringLiteral(string text)
    {
        return NgElement.FromStringLiteral(text);
    }

    #endregion
}
