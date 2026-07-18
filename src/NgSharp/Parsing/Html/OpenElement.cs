using System.Collections.Generic;

namespace NgSharp.Parsing
{
    // A tree-builder stack frame: an element whose children are still being collected.
    internal sealed class OpenElement
    {
        public OpenElement(string name, IReadOnlyList<HtmlAttribute> attributes)
        {
            Name = name;
            Attributes = attributes;
            Children = new List<HtmlNode>();
        }

        public string Name { get; }

        public IReadOnlyList<HtmlAttribute> Attributes { get; }

        public List<HtmlNode> Children { get; }
    }
}
