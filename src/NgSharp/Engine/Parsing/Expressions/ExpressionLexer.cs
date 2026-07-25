using System.Collections.Generic;

namespace NgSharp.Parsing;

internal static class ExpressionLexer
{
    public static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>(input.Length / 3 + 2);
        var i = 0;

        while (i < input.Length)
        {
            var ch = input[i];

            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            // '$' starts the loop variables ($index, $count, $first, $last) — lexed as ordinary paths.
            if (char.IsLetter(ch) || ch == '_' || ch == '$')
            {
                var start = i;
                // A path token carries '[n]' indices and optional chaining '?.': resolution is already
                // null-safe, so '?.' is consumed and normalized to '.'; a lone '?' stays for the ternary.
                while (i < input.Length
                    && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '.' || input[i] == '$'
                        || input[i] == '[' || input[i] == ']'
                        || (input[i] == '?' && i + 1 < input.Length && input[i + 1] == '.')))
                {
                    i++;
                }

                var path = input.Substring(start, i - start);
                var guarded = path.IndexOf('?') >= 0;
                if (guarded)
                {
                    path = path.Replace("?.", ".");
                }

                tokens.Add(new Token(TokenKind.Identifier, path, guarded));
                continue;
            }

            // A leading '-' is the parser's unary minus, never part of the literal ('A-3' is subtraction).
            if (char.IsDigit(ch))
            {
                var start = i;
                while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Number, input.Substring(start, i - start)));
                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                var quote = ch;
                i++;

                var contentStart = i;
                while (i < input.Length && input[i] != quote)
                {
                    i++;
                }

                var content = input.Substring(contentStart, i - contentStart);
                if (i < input.Length)
                {
                    i++; // closing quote
                }

                tokens.Add(new Token(TokenKind.String, content));
                continue;
            }

            // Two-char operators before the single-char switch ('<=' must not lex as '<' then '=').
            if (i + 1 < input.Length)
            {
                var next = input[i + 1];

                if (next == '=' && (ch == '=' || ch == '!' || ch == '<' || ch == '>'))
                {
                    var op = ch == '=' ? "==" : ch == '!' ? "!=" : ch == '<' ? "<=" : ">=";
                    tokens.Add(new Token(TokenKind.Operator, op));
                    i += 2;
                    continue;
                }

                if (ch == '&' && next == '&')
                {
                    tokens.Add(new Token(TokenKind.And, "&&"));
                    i += 2;
                    continue;
                }

                if (ch == '|' && next == '|')
                {
                    tokens.Add(new Token(TokenKind.Or, "||"));
                    i += 2;
                    continue;
                }
            }

            switch (ch)
            {
                case '<':
                    tokens.Add(new Token(TokenKind.Operator, "<"));
                    break;
                case '>':
                    tokens.Add(new Token(TokenKind.Operator, ">"));
                    break;
                case '!':
                    tokens.Add(new Token(TokenKind.Not, "!"));
                    break;
                case '+':
                    tokens.Add(new Token(TokenKind.Arithmetic, "+"));
                    break;
                case '-':
                    tokens.Add(new Token(TokenKind.Arithmetic, "-"));
                    break;
                case '*':
                    tokens.Add(new Token(TokenKind.Arithmetic, "*"));
                    break;
                case '/':
                    tokens.Add(new Token(TokenKind.Arithmetic, "/"));
                    break;
                case '%':
                    tokens.Add(new Token(TokenKind.Arithmetic, "%"));
                    break;
                case '|':
                    tokens.Add(new Token(TokenKind.Pipe, "|"));
                    break;
                case '?':
                    tokens.Add(new Token(TokenKind.Question, "?"));
                    break;
                case ':':
                    tokens.Add(new Token(TokenKind.Colon, ":"));
                    break;
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "("));
                    break;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")"));
                    break;
            }

            i++;
        }

        tokens.Add(new Token(TokenKind.End, string.Empty));

        return tokens;
    }
}
