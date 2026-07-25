using System;
using System.Text;

namespace NgSharp.Rendering;

// HTML entity-escaping for rendered output. Two flavours per sink:
//   * data (interpolated / bound values) -> FULL escaping: every '&' becomes '&amp;'.
//   * static template text -> ENTITY-AWARE: authored entities (&nbsp;, &#233;, …) are preserved,
//     only a bare '&' (and '<'/'>' or the '"' delimiter) is escaped.
// Regex-free by design so the trimmer can drop System.Text.RegularExpressions.
internal static class HtmlEscaper
{
    #region Fields

    // The characters escaping touches: '&', '<', '>', and U+00A0 (a non-breaking-space char, emitted
    // as the &nbsp; entity). A normal ASCII space is NOT in this set and passes through untouched.
    private static readonly char[] Specials = { '&', '<', '>', '\u00A0' };

    #endregion

    #region Public methods

    // Full escaping for interpolated *data* — every & becomes &amp; (the value is not markup).
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny(Specials) < 0)
        {
            return value;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\u00A0", "&nbsp;");
    }

    // The pipe span fast path's write-out \u2014 must stay character-for-character identical to Escape(string).
    public static void AppendEscaped(PooledCharWriter builder, ReadOnlySpan<char> value)
    {
        while (value.IsEmpty == false)
        {
            var next = value.IndexOfAny(Specials);
            if (next < 0)
            {
                builder.Append(value);

                return;
            }

            builder.Append(value.Slice(0, next));

            switch (value[next])
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                default:
                    builder.Append("&nbsp;");
                    break;
            }

            value = value.Slice(next + 1);
        }
    }

    // Static (authored) template text: preserve existing entities, escape only a bare & and </>.
    public static string EscapeText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny(Specials) < 0)
        {
            return value;
        }

        var result = value.IndexOf('&') >= 0 ? EscapeBareAmpersands(value) : value;

        return result
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\u00A0", "&nbsp;");
    }

    // Full escaping for a dynamically-bound (data) attribute value.
    public static string EscapeAttribute(string value)
    {
        return Escape(value).Replace("\"", "&quot;");
    }

    // Static (authored) attribute value: preserve authored entities, escape only a bare & and the quote delimiter.
    public static string EscapeAttributeText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOf('&') < 0 && value.IndexOf('"') < 0)
        {
            return value;
        }

        return EscapeBareAmpersands(value).Replace("\"", "&quot;");
    }

    #endregion

    #region Private methods

    // Replaces each bare '&' — one that does NOT begin a valid character entity — with "&amp;".
    private static string EscapeBareAmpersands(string value)
    {
        var first = value.IndexOf('&');
        if (first < 0)
        {
            return value;
        }

        StringBuilder builder = null;
        var last = 0;

        for (var i = first; i >= 0; i = value.IndexOf('&', i + 1))
        {
            if (StartsEntity(value, i))
            {
                continue;
            }

            builder ??= new StringBuilder(value.Length + 8);
            builder.Append(value, last, i - last).Append("&amp;");
            last = i + 1;
        }

        if (builder is null)
        {
            return value;
        }

        builder.Append(value, last, value.Length - last);

        return builder.ToString();
    }

    // True when value[amp] == '&' begins a valid entity: #\d+;  |  #x[0-9a-fA-F]+;  |  [A-Za-z][A-Za-z0-9]*;
    private static bool StartsEntity(string value, int amp)
    {
        var length = value.Length;
        var j = amp + 1;
        if (j >= length)
        {
            return false;
        }

        if (value[j] == '#')
        {
            j++;

            if (j < length && value[j] == 'x')
            {
                j++;
                var hexStart = j;
                while (j < length && IsHex(value[j]))
                {
                    j++;
                }

                return j > hexStart && j < length && value[j] == ';';
            }

            var digitStart = j;
            while (j < length && char.IsDigit(value[j]))
            {
                j++;
            }

            return j > digitStart && j < length && value[j] == ';';
        }

        if (IsAsciiLetter(value[j]))
        {
            j++;
            while (j < length && IsAsciiLetterOrDigit(value[j]))
            {
                j++;
            }

            return j < length && value[j] == ';';
        }

        return false;
    }

    private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static bool IsAsciiLetterOrDigit(char c) => IsAsciiLetter(c) || (c >= '0' && c <= '9');

    #endregion
}
