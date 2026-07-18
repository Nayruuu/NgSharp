using System.Collections.Generic;
using System.Text.Json;

using NgSharp.Ast;

namespace NgSharp.Parsing
{
    internal static class ExpressionParser
    {
        public static Expression Parse(string input)
        {
            var tokens = ExpressionLexer.Tokenize(input);
            var pos = 0;

            return ParseTernary(tokens, ref pos);
        }

        // ternary := logicalOr ('?' ternary ':' ternary)?
        private static Expression ParseTernary(IReadOnlyList<Token> tokens, ref int pos)
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

                var whenFalse = ParseTernary(tokens, ref pos);

                return new TernaryExpression(condition, whenTrue, whenFalse);
            }

            return condition;
        }

        // logicalOr := logicalAnd ('||' logicalAnd)*
        private static Expression ParseLogicalOr(IReadOnlyList<Token> tokens, ref int pos)
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
        private static Expression ParseLogicalAnd(IReadOnlyList<Token> tokens, ref int pos)
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
        private static Expression ParsePipe(IReadOnlyList<Token> tokens, ref int pos)
        {
            var source = ParseComparison(tokens, ref pos);

            while (Peek(tokens, pos).Kind == TokenKind.Pipe)
            {
                pos++;

                var name = Peek(tokens, pos).Text;
                if (Peek(tokens, pos).Kind == TokenKind.Identifier)
                {
                    pos++;
                }

                var arguments = new List<Expression>();
                while (Peek(tokens, pos).Kind == TokenKind.Colon)
                {
                    pos++;
                    arguments.Add(ParsePrimary(tokens, ref pos));
                }

                source = new PipeExpression(source, name, arguments);
            }

            return source;
        }

        // comparison := primary (OP primary)?
        private static Expression ParseComparison(IReadOnlyList<Token> tokens, ref int pos)
        {
            var left = ParsePrimary(tokens, ref pos);

            if (Peek(tokens, pos).Kind == TokenKind.Operator)
            {
                var op = tokens[pos].Text;
                pos++;

                var right = ParsePrimary(tokens, ref pos);

                return new ComparisonExpression(left, op, right);
            }

            return left;
        }

        private static Expression ParsePrimary(IReadOnlyList<Token> tokens, ref int pos)
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
                    return new PathExpression(token.Text);

                case TokenKind.LParen:
                    pos++;
                    var inner = ParseTernary(tokens, ref pos);
                    if (Peek(tokens, pos).Kind == TokenKind.RParen)
                    {
                        pos++;
                    }
                    return inner;

                default:
                    pos++;
                    return new LiteralExpression(NgElement.Parse("null"));
            }
        }

        private static Token Peek(IReadOnlyList<Token> tokens, int pos)
            => pos < tokens.Count ? tokens[pos] : tokens[tokens.Count - 1];

        // JSON numbers/strings are culture-invariant (always '.' decimals, always quoted),
        // so building literals through JsonElement avoids NgElement.Parse's culture-sensitive
        // coercion (which would read '123' as a number, or misparse decimals under fr-FR).
        private static NgElement NumberLiteral(string text)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                return NgElement.FromJson(doc.RootElement.Clone());
            }
            catch (JsonException)
            {
                // Malformed number (e.g. "1.2.3") — degrade to a string literal instead of crashing.
                return StringLiteral(text);
            }
        }

        private static NgElement StringLiteral(string text)
        {
            return NgElement.FromStringLiteral(text);
        }
    }
}
