// Reference pipeline (tests only): see HtmlTreeBuilder.cs for why this lives in the test assembly.
#nullable disable
using System.Collections.Generic;

namespace NgSharp.Parsing;

internal sealed class HtmlToken
{
    private static readonly IReadOnlyList<HtmlAttribute> NoAttributes = new HtmlAttribute[0];

    public HtmlTokenKind Kind { get; }

    // Text/Comment content, or the tag name for OpenTag/CloseTag.
    public string Value { get; }

    public IReadOnlyList<HtmlAttribute> Attributes { get; }

    public bool SelfClosing { get; }

    private HtmlToken(HtmlTokenKind kind, string value, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing)
    {
        Kind = kind;
        Value = value;
        Attributes = attributes ?? NoAttributes;
        SelfClosing = selfClosing;
    }

    public static HtmlToken Text(string text) => new HtmlToken(HtmlTokenKind.Text, text, null, false);

    public static HtmlToken Comment(string text) => new HtmlToken(HtmlTokenKind.Comment, text, null, false);

    public static HtmlToken OpenTag(string name, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing)
        => new HtmlToken(HtmlTokenKind.OpenTag, name, attributes, selfClosing);

    public static HtmlToken CloseTag(string name) => new HtmlToken(HtmlTokenKind.CloseTag, name, null, false);
}
