using System.Collections.Generic;

namespace NgSharp.Parsing
{
    // A purpose-built HTML *template* tokenizer (replaces AngleSharp's parse). Syntax only:
    // tags, attributes, text and comments, plus rawtext for <script>/<style>. It is deliberately
    // NOT an HTML5 parser — no error recovery, foster-parenting or implied end tags. Void-ness and
    // tree structure belong to the tree-builder; interpolation {{ }} stays inside text tokens.
    internal static class HtmlLexer
    {
        public static IReadOnlyList<HtmlToken> Tokenize(string html)
        {
            var tokens = new List<HtmlToken>();
            if (string.IsNullOrEmpty(html))
            {
                return tokens;
            }

            var i = 0;
            while (i < html.Length)
            {
                if (html[i] == '<' && IsMarkupStart(html, i))
                {
                    if (StartsWith(html, i, "<!--"))
                    {
                        i = ReadComment(html, i, tokens);
                    }
                    else if (html[i + 1] == '!')
                    {
                        // Declaration (<!doctype …>): dropped from the output, as AngleSharp did.
                        i = SkipDeclaration(html, i);
                    }
                    else if (html[i + 1] == '/')
                    {
                        i = ReadCloseTag(html, i, tokens);
                    }
                    else
                    {
                        i = ReadOpenTag(html, i, tokens);
                    }
                }
                else
                {
                    i = ReadText(html, i, tokens);
                }
            }

            return tokens;
        }

        // A '<' opens markup only when followed by a name-start, '/', or '!'. Otherwise (e.g. "a < b")
        // it is literal text.
        private static bool IsMarkupStart(string s, int i)
        {
            if (i + 1 >= s.Length)
            {
                return false;
            }

            var c = s[i + 1];
            return c == '/' || c == '!' || IsNameStart(c);
        }

        private static bool IsNameStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static int ReadText(string s, int i, List<HtmlToken> tokens)
        {
            var start = i;
            while (i < s.Length && !(s[i] == '<' && IsMarkupStart(s, i)))
            {
                i++;
            }

            tokens.Add(HtmlToken.Text(s.Substring(start, i - start)));
            return i;
        }

        private static int SkipDeclaration(string s, int i)
        {
            var end = s.IndexOf('>', i);
            return end < 0 ? s.Length : end + 1;
        }

        private static int ReadComment(string s, int i, List<HtmlToken> tokens)
        {
            var start = i + 4;
            var end = s.IndexOf("-->", start, System.StringComparison.Ordinal);
            if (end < 0)
            {
                tokens.Add(HtmlToken.Comment(s.Substring(start)));
                return s.Length;
            }

            tokens.Add(HtmlToken.Comment(s.Substring(start, end - start)));
            return end + 3;
        }

        private static int ReadCloseTag(string s, int i, List<HtmlToken> tokens)
        {
            var j = i + 2;
            var nameStart = j;
            while (j < s.Length && s[j] != '>' && !IsWhitespace(s[j]))
            {
                j++;
            }

            var name = s.Substring(nameStart, j - nameStart);

            while (j < s.Length && s[j] != '>')
            {
                j++;
            }

            if (j < s.Length)
            {
                j++;
            }

            tokens.Add(HtmlToken.CloseTag(name));
            return j;
        }

        private static int ReadOpenTag(string s, int i, List<HtmlToken> tokens)
        {
            var j = i + 1;
            var nameStart = j;
            while (j < s.Length && !IsWhitespace(s[j]) && s[j] != '>' && s[j] != '/')
            {
                j++;
            }

            var name = s.Substring(nameStart, j - nameStart);

            var attributes = new List<HtmlAttribute>();
            var selfClosing = false;

            while (j < s.Length)
            {
                j = SkipWhitespace(s, j);
                if (j >= s.Length)
                {
                    break;
                }

                if (s[j] == '>')
                {
                    j++;
                    break;
                }

                if (s[j] == '/' && j + 1 < s.Length && s[j + 1] == '>')
                {
                    selfClosing = true;
                    j += 2;
                    break;
                }

                if (s[j] == '/')
                {
                    j++;
                    continue;
                }

                var attrStart = j;
                while (j < s.Length && !IsWhitespace(s[j]) && s[j] != '=' && s[j] != '>' && s[j] != '/')
                {
                    j++;
                }

                var attrName = s.Substring(attrStart, j - attrStart);

                j = SkipWhitespace(s, j);
                var value = string.Empty;
                if (j < s.Length && s[j] == '=')
                {
                    j++;
                    j = SkipWhitespace(s, j);
                    value = ReadAttributeValue(s, ref j);
                }

                attributes.Add(new HtmlAttribute(attrName, value));
            }

            tokens.Add(HtmlToken.OpenTag(name, attributes, selfClosing));

            if (!selfClosing && IsRawText(name))
            {
                j = ReadRawText(s, j, name, tokens);
            }

            return j;
        }

        private static string ReadAttributeValue(string s, ref int j)
        {
            if (j >= s.Length)
            {
                return string.Empty;
            }

            var quote = s[j];
            if (quote == '"' || quote == '\'')
            {
                j++;
                var start = j;
                while (j < s.Length && s[j] != quote)
                {
                    j++;
                }

                var value = s.Substring(start, j - start);
                if (j < s.Length)
                {
                    j++;
                }

                return value;
            }

            var unquotedStart = j;
            while (j < s.Length && !IsWhitespace(s[j]) && s[j] != '>')
            {
                j++;
            }

            return s.Substring(unquotedStart, j - unquotedStart);
        }

        // Inside <script>/<style> everything up to the matching close tag is literal text.
        private static int ReadRawText(string s, int i, string tagName, List<HtmlToken> tokens)
        {
            var end = IndexOfIgnoreCase(s, "</" + tagName, i);
            if (end < 0)
            {
                tokens.Add(HtmlToken.Text(s.Substring(i)));
                return s.Length;
            }

            tokens.Add(HtmlToken.Text(s.Substring(i, end - i)));
            return end;
        }

        private static bool IsRawText(string name)
            => EqualsIgnoreCase(name, "script") || EqualsIgnoreCase(name, "style");

        private static bool StartsWith(string s, int i, string token)
        {
            if (i + token.Length > s.Length)
            {
                return false;
            }

            for (var k = 0; k < token.Length; k++)
            {
                if (s[i + k] != token[k])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EqualsIgnoreCase(string name, string lower)
        {
            if (name.Length != lower.Length)
            {
                return false;
            }

            for (var k = 0; k < name.Length; k++)
            {
                if (char.ToLowerInvariant(name[k]) != lower[k])
                {
                    return false;
                }
            }

            return true;
        }

        private static int IndexOfIgnoreCase(string s, string token, int from)
        {
            for (var k = from; k + token.Length <= s.Length; k++)
            {
                var match = true;
                for (var m = 0; m < token.Length; m++)
                {
                    if (char.ToLowerInvariant(s[k + m]) != char.ToLowerInvariant(token[m]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return k;
                }
            }

            return -1;
        }

        private static int SkipWhitespace(string s, int j)
        {
            while (j < s.Length && IsWhitespace(s[j]))
            {
                j++;
            }

            return j;
        }

        private static bool IsWhitespace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f';
    }
}
