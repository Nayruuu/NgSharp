// Reference pipeline (tests only): see HtmlTreeBuilder.cs for why this lives in the test assembly.
#nullable disable
using System.Collections.Generic;

namespace NgSharp.Parsing;

// A tree-builder stack frame: an element whose children are still being collected.
internal sealed class OpenElement
{
    public string Name { get; }

    public IReadOnlyList<HtmlAttribute> Attributes { get; }

    public List<HtmlNode> Children { get; }

    public OpenElement(string name, IReadOnlyList<HtmlAttribute> attributes)
    {
        Name = name;
        Attributes = attributes;
        Children = new List<HtmlNode>();
    }
}
