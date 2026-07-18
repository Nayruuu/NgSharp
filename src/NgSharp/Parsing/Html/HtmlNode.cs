using System.Collections.Generic;

namespace NgSharp.Parsing
{
    // A lightweight parse-tree node (replaces AngleSharp's INode/IElement for template parsing).
    internal sealed class HtmlNode
    {
        private static readonly IReadOnlyList<HtmlAttribute> NoAttributes = new HtmlAttribute[0];
        private static readonly IReadOnlyList<HtmlNode> NoChildren = new HtmlNode[0];

        private HtmlNode(HtmlNodeType nodeType, string name, string text, IReadOnlyList<HtmlAttribute> attributes, IReadOnlyList<HtmlNode> children)
        {
            NodeType = nodeType;
            Name = name;
            Text = text;
            Attributes = attributes ?? NoAttributes;
            Children = children ?? NoChildren;
        }

        public HtmlNodeType NodeType { get; }

        // Tag name for Element; null otherwise.
        public string Name { get; }

        // Content for Text/Comment; null for Element.
        public string Text { get; }

        public IReadOnlyList<HtmlAttribute> Attributes { get; }

        public IReadOnlyList<HtmlNode> Children { get; }

        public static HtmlNode Element(string name, IReadOnlyList<HtmlAttribute> attributes, IReadOnlyList<HtmlNode> children)
            => new HtmlNode(HtmlNodeType.Element, name, null, attributes, children);

        public static HtmlNode TextNode(string text) => new HtmlNode(HtmlNodeType.Text, null, text, null, null);

        public static HtmlNode CommentNode(string text) => new HtmlNode(HtmlNodeType.Comment, null, text, null, null);

        public string GetAttribute(string name)
        {
            foreach (var attribute in Attributes)
            {
                if (attribute.Name == name)
                {
                    return attribute.Value;
                }
            }

            return null;
        }

        public bool HasAttribute(string name)
        {
            foreach (var attribute in Attributes)
            {
                if (attribute.Name == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
