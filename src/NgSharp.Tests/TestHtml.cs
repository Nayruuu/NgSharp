using System;
using System.Text;

namespace NgSharp.Tests;

// Whitespace normalizer for HTML comparisons in tests — collapses insignificant whitespace so
// assertions can be layout-insensitive. Moved out of the library's public API (HtmlBuilder.MinifyHtml):
// the render pipeline never minifies, and the tests were its only consumer. The four passes are the
// regex-free hand scanners, byte-identical to the old Regex.Replace chain they replaced.
internal static class TestHtml
{
    public static string Minify(string html)
    {
        var result = RemoveChars(html, '\r', '\n', '\t');   // \r|\n|\t          -> ""  (drop line breaks/tabs)
        result = CollapseTagGap(result);                    // >\s+<             -> ><  (space between tags)
        result = StripSpaceBetweenTags(result);             // (?<=>)\s+(?=<)    -> ""  (empty text indentation)
        result = CollapseWhitespaceRuns(result);            // \s{2,}            -> " " (runs to a single space)

        return result.Trim();
    }

    private static string RemoveChars(string source, char first, char second, char third)
    {
        StringBuilder sb = null;

        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch == first || ch == second || ch == third)
            {
                sb ??= new StringBuilder(source.Length).Append(source, 0, i);
            }
            else
            {
                sb?.Append(ch);
            }
        }

        return sb?.ToString() ?? source;
    }

    // >\s+<  ->  ><
    private static string CollapseTagGap(string source)
    {
        StringBuilder sb = null;
        var i = 0;

        while (i < source.Length)
        {
            if (source[i] == '>')
            {
                var j = i + 1;
                while (j < source.Length && char.IsWhiteSpace(source[j]))
                {
                    j++;
                }

                if (j > i + 1 && j < source.Length && source[j] == '<')
                {
                    sb ??= new StringBuilder(source.Length).Append(source, 0, i);
                    sb.Append("><");
                    i = j + 1;
                    continue;
                }
            }

            sb?.Append(source[i]);
            i++;
        }

        return sb?.ToString() ?? source;
    }

    // (?<=>)\s+(?=<)  ->  ""  (drop a whitespace run bounded by '>' before and '<' after)
    private static string StripSpaceBetweenTags(string source)
    {
        StringBuilder sb = null;
        var i = 0;

        while (i < source.Length)
        {
            if (char.IsWhiteSpace(source[i]))
            {
                var j = i;
                while (j < source.Length && char.IsWhiteSpace(source[j]))
                {
                    j++;
                }

                if (i > 0 && source[i - 1] == '>' && j < source.Length && source[j] == '<')
                {
                    sb ??= new StringBuilder(source.Length).Append(source, 0, i);
                    i = j;   // drop the run
                    continue;
                }

                sb?.Append(source, i, j - i);
                i = j;
                continue;
            }

            sb?.Append(source[i]);
            i++;
        }

        return sb?.ToString() ?? source;
    }

    // \s{2,}  ->  " "  (a run of 2+ whitespace becomes a single space; a lone whitespace char is kept)
    private static string CollapseWhitespaceRuns(string source)
    {
        StringBuilder sb = null;
        var i = 0;

        while (i < source.Length)
        {
            if (char.IsWhiteSpace(source[i]))
            {
                var j = i;
                while (j < source.Length && char.IsWhiteSpace(source[j]))
                {
                    j++;
                }

                if (j - i >= 2)
                {
                    sb ??= new StringBuilder(source.Length).Append(source, 0, i);
                    sb.Append(' ');
                }
                else
                {
                    sb?.Append(source[i]);
                }

                i = j;
                continue;
            }

            sb?.Append(source[i]);
            i++;
        }

        return sb?.ToString() ?? source;
    }
}
