// Reference pipeline (tests only): see HtmlTreeBuilder.cs for why this lives in the test assembly.
#nullable disable
using System.Collections.Generic;

namespace NgSharp.Parsing;

// A purpose-built HTML *template* tokenizer — syntax only: tags, attributes, text, comments, rawtext
// for <script>/<style>. Deliberately NOT an HTML5 parser (no error recovery, foster-parenting or
// implied end tags); void-ness and tree structure belong to the tree-builder; {{ }} stays inside text.
internal static class HtmlLexer
{
    public static IReadOnlyList<HtmlToken> Tokenize(string html)
    {
        var tokens = new List<HtmlToken>((html?.Length ?? 0) / 8 + 4);

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
                    // Declaration (<!doctype …>): dropped from the output.
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

    // A '<' opens markup only when followed by a name-start, '/', or '!'; otherwise ("a < b") it is text.
    private static bool IsMarkupStart(string source, int i)
    {
        if (i + 1 >= source.Length)
        {
            return false;
        }

        var ch = source[i + 1];

        return ch == '/' || ch == '!' || IsNameStart(ch);
    }

    private static bool IsNameStart(char ch) => (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');

    private static int ReadText(string source, int i, List<HtmlToken> tokens)
    {
        var start = i;
        while (i < source.Length && (source[i] == '<' && IsMarkupStart(source, i)) == false)
        {
            i++;
        }

        tokens.Add(HtmlToken.Text(source.Substring(start, i - start)));

        return i;
    }

    private static int SkipDeclaration(string source, int i)
    {
        var end = source.IndexOf('>', i);

        return end < 0 ? source.Length : end + 1;
    }

    private static int ReadComment(string source, int i, List<HtmlToken> tokens)
    {
        var start = i + 4;
        var end = source.IndexOf("-->", start, System.StringComparison.Ordinal);
        if (end < 0)
        {
            tokens.Add(HtmlToken.Comment(source.Substring(start)));

            return source.Length;
        }

        tokens.Add(HtmlToken.Comment(source.Substring(start, end - start)));

        return end + 3;
    }

    private static int ReadCloseTag(string source, int i, List<HtmlToken> tokens)
    {
        var j = i + 2;
        var nameStart = j;
        while (j < source.Length && source[j] != '>' && IsWhitespace(source[j]) == false)
        {
            j++;
        }

        var name = source.Substring(nameStart, j - nameStart);

        while (j < source.Length && source[j] != '>')
        {
            j++;
        }

        if (j < source.Length)
        {
            j++;
        }

        tokens.Add(HtmlToken.CloseTag(name));

        return j;
    }

    private static int ReadOpenTag(string source, int i, List<HtmlToken> tokens)
    {
        var j = i + 1;
        var nameStart = j;
        while (j < source.Length && IsWhitespace(source[j]) == false && source[j] != '>' && source[j] != '/')
        {
            j++;
        }

        var name = source.Substring(nameStart, j - nameStart);

        List<HtmlAttribute> attributes = null;
        var selfClosing = false;

        while (j < source.Length)
        {
            j = SkipWhitespace(source, j);
            if (j >= source.Length)
            {
                break;
            }

            if (source[j] == '>')
            {
                j++;
                break;
            }

            if (source[j] == '/' && j + 1 < source.Length && source[j + 1] == '>')
            {
                selfClosing = true;
                j += 2;
                break;
            }

            if (source[j] == '/')
            {
                j++;
                continue;
            }

            var attrStart = j;
            while (j < source.Length && IsWhitespace(source[j]) == false && source[j] != '=' && source[j] != '>' && source[j] != '/')
            {
                j++;
            }

            var attrName = source.Substring(attrStart, j - attrStart);

            j = SkipWhitespace(source, j);
            var value = string.Empty;
            if (j < source.Length && source[j] == '=')
            {
                j++;
                j = SkipWhitespace(source, j);
                value = ReadAttributeValue(source, ref j);
            }

            (attributes ??= new List<HtmlAttribute>()).Add(new HtmlAttribute(attrName, value));
        }

        tokens.Add(HtmlToken.OpenTag(name, attributes, selfClosing));

        if (selfClosing == false && IsRawText(name))
        {
            j = ReadRawText(source, j, name, tokens);
        }

        return j;
    }

    private static string ReadAttributeValue(string source, ref int j)
    {
        if (j >= source.Length)
        {
            return string.Empty;
        }

        var quote = source[j];
        if (quote == '"' || quote == '\'')
        {
            j++;

            var start = j;
            while (j < source.Length && source[j] != quote)
            {
                j++;
            }

            var value = source.Substring(start, j - start);
            if (j < source.Length)
            {
                j++;
            }

            return value;
        }

        var unquotedStart = j;
        while (j < source.Length && IsWhitespace(source[j]) == false && source[j] != '>')
        {
            j++;
        }

        return source.Substring(unquotedStart, j - unquotedStart);
    }

    // Inside <script>/<style> everything up to the matching close tag is literal text.
    private static int ReadRawText(string source, int i, string tagName, List<HtmlToken> tokens)
    {
        var end = IndexOfIgnoreCase(source, "</" + tagName, i);
        if (end < 0)
        {
            tokens.Add(HtmlToken.Text(source.Substring(i)));

            return source.Length;
        }

        tokens.Add(HtmlToken.Text(source.Substring(i, end - i)));

        return end;
    }

    private static bool IsRawText(string name)
        => EqualsIgnoreCase(name, "script") || EqualsIgnoreCase(name, "style");

    private static bool StartsWith(string source, int i, string token)
    {
        if (i + token.Length > source.Length)
        {
            return false;
        }

        for (var k = 0; k < token.Length; k++)
        {
            if (source[i + k] != token[k])
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

    private static int IndexOfIgnoreCase(string source, string token, int from)
    {
        for (var k = from; k + token.Length <= source.Length; k++)
        {
            var match = true;
            for (var m = 0; m < token.Length; m++)
            {
                if (char.ToLowerInvariant(source[k + m]) != char.ToLowerInvariant(token[m]))
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

    private static int SkipWhitespace(string source, int j)
    {
        while (j < source.Length && IsWhitespace(source[j]))
        {
            j++;
        }

        return j;
    }

    private static bool IsWhitespace(char ch) => ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == '\f';
}
