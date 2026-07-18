using System.Collections.Generic;

namespace NgSharp.Parsing
{
    internal sealed class HtmlToken
    {
        private static readonly IReadOnlyList<HtmlAttribute> NoAttributes = new HtmlAttribute[0];

        private HtmlToken(HtmlTokenKind kind, string value, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing)
        {
            Kind = kind;
            Value = value;
            Attributes = attributes ?? NoAttributes;
            SelfClosing = selfClosing;
        }

        public HtmlTokenKind Kind { get; }

        // Text/Comment content, or the tag name for OpenTag/CloseTag.
        public string Value { get; }

        public IReadOnlyList<HtmlAttribute> Attributes { get; }

        public bool SelfClosing { get; }

        public static HtmlToken Text(string text) => new HtmlToken(HtmlTokenKind.Text, text, null, false);

        public static HtmlToken Comment(string text) => new HtmlToken(HtmlTokenKind.Comment, text, null, false);

        public static HtmlToken OpenTag(string name, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing)
            => new HtmlToken(HtmlTokenKind.OpenTag, name, attributes, selfClosing);

        public static HtmlToken CloseTag(string name) => new HtmlToken(HtmlTokenKind.CloseTag, name, null, false);
    }
}
