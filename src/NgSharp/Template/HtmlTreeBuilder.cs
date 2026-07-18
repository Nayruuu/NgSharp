using System;
using System.Collections.Generic;

namespace NgSharp.Template
{
    internal enum HtmlNodeType
    {
        Element,
        Text,
        Comment
    }

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

    // Builds the HtmlNode tree from the token stream. Void-aware; lenient (no HTML5 error-recovery):
    // a mismatched close implicitly closes intervening elements, a stray close is ignored, and
    // anything still open at EOF is auto-closed.
    internal static class HtmlTreeBuilder
    {
        private static readonly HashSet<string> VoidElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
        };

        public static IReadOnlyList<HtmlNode> Build(IReadOnlyList<HtmlToken> tokens)
        {
            var roots = new List<HtmlNode>();
            var stack = new Stack<OpenElement>();

            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case HtmlTokenKind.Text:
                        Add(HtmlNode.TextNode(token.Value), stack, roots);
                        break;

                    case HtmlTokenKind.Comment:
                        Add(HtmlNode.CommentNode(token.Value), stack, roots);
                        break;

                    case HtmlTokenKind.OpenTag:
                        if (token.SelfClosing || VoidElements.Contains(token.Value))
                        {
                            Add(HtmlNode.Element(token.Value, token.Attributes, Array.Empty<HtmlNode>()), stack, roots);
                        }
                        else
                        {
                            stack.Push(new OpenElement(token.Value, token.Attributes));
                        }

                        break;

                    case HtmlTokenKind.CloseTag:
                        CloseTag(token.Value, stack, roots);
                        break;
                }
            }

            while (stack.Count > 0)
            {
                Finalize(stack, roots);
            }

            return roots;
        }

        private static void Add(HtmlNode node, Stack<OpenElement> stack, List<HtmlNode> roots)
        {
            if (stack.Count > 0)
            {
                stack.Peek().Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        private static void CloseTag(string name, Stack<OpenElement> stack, List<HtmlNode> roots)
        {
            if (!Contains(stack, name))
            {
                return;
            }

            while (stack.Count > 0)
            {
                var closed = Finalize(stack, roots);
                if (NameEquals(closed, name))
                {
                    break;
                }
            }
        }

        // Materializes the top open element and appends it to its parent (the new top) or the roots.
        private static string Finalize(Stack<OpenElement> stack, List<HtmlNode> roots)
        {
            var top = stack.Pop();
            var node = HtmlNode.Element(top.Name, top.Attributes, top.Children);

            if (stack.Count > 0)
            {
                stack.Peek().Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }

            return top.Name;
        }

        private static bool Contains(Stack<OpenElement> stack, string name)
        {
            foreach (var open in stack)
            {
                if (NameEquals(open.Name, name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NameEquals(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private sealed class OpenElement
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
}
