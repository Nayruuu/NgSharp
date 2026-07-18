using System.Text.RegularExpressions;

namespace NgSharp.Rendering
{
    // HTML entity-escaping for rendered output. Two flavours per sink:
    //   * data (interpolated / bound values) -> FULL escaping: every '&' becomes '&amp;'.
    //   * static template text -> ENTITY-AWARE: authored entities (&nbsp;, &#233;, …) are preserved,
    //     only a bare '&' (and '<'/'>' or the '"' delimiter) is escaped.
    // Each method fast-returns the input unchanged when it holds none of the characters it would touch
    // (the common case for names, numbers and plain markup) — one scan instead of several Replace passes.
    internal static class HtmlEscaper
    {
        // Entity references already written in the template (&nbsp;, &amp;, &eacute;, &#233;) are
        // authored markup — preserve them; only a bare & matches.
        private static readonly Regex BareAmpersand = new Regex(@"&(?!#\d+;|#x[0-9a-fA-F]+;|[a-zA-Z][a-zA-Z0-9]*;)", RegexOptions.Compiled);

        // The characters escaping touches: '&', '<', '>', and U+00A0 (a non-breaking-space char, emitted
        // as the &nbsp; entity). A normal ASCII space is NOT in this set and passes through untouched.
        private static readonly char[] Specials = { '&', '<', '>', ' ' };

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
                .Replace(" ", "&nbsp;");
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

            // Only pay the compiled regex when there is actually a '&' to disambiguate.
            var result = value.IndexOf('&') >= 0 ? BareAmpersand.Replace(value, "&amp;") : value;

            return result
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace(" ", "&nbsp;");
        }

        // Full escaping for a dynamically-bound (data) attribute value.
        public static string EscapeAttribute(string value)
        {
            return Escape(value).Replace("\"", "&quot;");
        }

        // Static (authored) attribute value: preserve entities already written in the template
        // (&amp;, &eacute;), escape only a bare & and the quote delimiter.
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

            return BareAmpersand.Replace(value, "&amp;").Replace("\"", "&quot;");
        }
    }
}
