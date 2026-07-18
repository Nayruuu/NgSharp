using System.Collections.Generic;

namespace NgSharp.Expressions
{
    public static class Lexer
    {
        public static IReadOnlyList<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            var i = 0;

            while (i < input.Length)
            {
                var c = input[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    var start = i;
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '.'))
                    {
                        i++;
                    }
                    tokens.Add(new Token(TokenKind.Identifier, input.Substring(start, i - start), start));
                    continue;
                }

                // A '-' immediately before a digit is a negative numeric literal (the grammar has
                // no binary minus, so '-' is only ever unary here).
                if (char.IsDigit(c) || (c == '-' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
                {
                    var start = i;
                    if (c == '-')
                    {
                        i++;
                    }
                    while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.'))
                    {
                        i++;
                    }
                    tokens.Add(new Token(TokenKind.Number, input.Substring(start, i - start), start));
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    var quote = c;
                    var start = i;
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
                    tokens.Add(new Token(TokenKind.String, content, start));
                    continue;
                }

                if (i + 1 < input.Length)
                {
                    var two = input.Substring(i, 2);
                    if (two == "==" || two == "!=" || two == "<=" || two == ">=")
                    {
                        tokens.Add(new Token(TokenKind.Operator, two, i));
                        i += 2;
                        continue;
                    }
                    if (two == "&&")
                    {
                        tokens.Add(new Token(TokenKind.And, "&&", i));
                        i += 2;
                        continue;
                    }
                    if (two == "||")
                    {
                        tokens.Add(new Token(TokenKind.Or, "||", i));
                        i += 2;
                        continue;
                    }
                }

                switch (c)
                {
                    case '<':
                    case '>':
                        tokens.Add(new Token(TokenKind.Operator, c.ToString(), i));
                        break;
                    case '|':
                        tokens.Add(new Token(TokenKind.Pipe, "|", i));
                        break;
                    case '?':
                        tokens.Add(new Token(TokenKind.Question, "?", i));
                        break;
                    case ':':
                        tokens.Add(new Token(TokenKind.Colon, ":", i));
                        break;
                    case '(':
                        tokens.Add(new Token(TokenKind.LParen, "(", i));
                        break;
                    case ')':
                        tokens.Add(new Token(TokenKind.RParen, ")", i));
                        break;
                }

                i++;
            }

            tokens.Add(new Token(TokenKind.End, string.Empty, input.Length));
            return tokens;
        }
    }
}
