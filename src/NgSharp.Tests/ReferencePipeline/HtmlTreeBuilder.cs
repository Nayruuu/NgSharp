// Reference pipeline (tests only): lives in the test assembly since the fused parser became the
// only shipped one; kept because the differential oracle (FusedParserDifferentialTests) renders
// through it. Void-ness comes from the lib's HtmlVoidElements — the exact set the fused parser uses.
#nullable disable
using System;
using System.Collections.Generic;

namespace NgSharp.Parsing;

// Builds the HtmlNode tree from the token stream. Void-aware; lenient (no HTML5 error-recovery):
// a mismatched close implicitly closes intervening elements, a stray close is ignored, and
// anything still open at EOF is auto-closed.
internal static class HtmlTreeBuilder
{
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
                    if (token.SelfClosing || HtmlVoidElements.Contains(token.Value))
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
        if (Contains(stack, name) == false)
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
}
