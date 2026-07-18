using System.Collections.Generic;
using System.Text.RegularExpressions;

using NgSharp.Ast;
using NgSharp.Parsing;

namespace NgSharp.Parsing
{
    internal static class TemplateParser
    {
        private static readonly Regex InterpolationPattern = new Regex(@"\{\{\s*(.*?)\s*\}\}", RegexOptions.Compiled);

        public static IReadOnlyList<TemplateNode> Parse(string html, IEnumerable<string> componentNames = null, IEnumerable<string> directiveNames = null)
            => ParseRoots(html, componentNames, directiveNames);

        // Kept for API compatibility. AngleSharp's <html>/<head>/<body> auto-wrapping (and other
        // browser-grade document normalization) is gone — the template is parsed exactly as written.
        public static IReadOnlyList<TemplateNode> ParseDocument(string html, IEnumerable<string> componentNames = null, IEnumerable<string> directiveNames = null)
            => ParseRoots(html, componentNames, directiveNames);

        private static IReadOnlyList<TemplateNode> ParseRoots(string html, IEnumerable<string> componentNames, IEnumerable<string> directiveNames)
        {
            var expanded = ControlFlowPreprocessor.Expand(html ?? string.Empty);
            var roots = HtmlTreeBuilder.Build(HtmlLexer.Tokenize(expanded));

            return ParseChildren(roots, Set(componentNames), Set(directiveNames));
        }

        private static HashSet<string> Set(IEnumerable<string> names)
            => names == null ? new HashSet<string>() : new HashSet<string>(names);

        private static IReadOnlyList<TemplateNode> ParseChildren(IReadOnlyList<HtmlNode> children, HashSet<string> components, HashSet<string> directives)
        {
            var nodes = new List<TemplateNode>();

            for (var idx = 0; idx < children.Count; idx++)
            {
                var child = children[idx];

                switch (child.NodeType)
                {
                    case HtmlNodeType.Element:
                        var node = BuildElement(child, components, directives);

                        // An @if element yields an outermost IfNode; fold any following
                        // [else-if]/[else] sibling(s) into it (idx advances past them).
                        if (node is IfNode ifNode)
                        {
                            var elseBranch = BuildElseChain(children, ref idx, components, directives);
                            if (elseBranch != null)
                            {
                                node = new IfNode(ifNode.Condition, ifNode.Body, elseBranch);
                            }
                        }

                        nodes.Add(node);
                        break;

                    case HtmlNodeType.Text:
                        AppendText(nodes, child.Text);
                        break;

                    case HtmlNodeType.Comment:
                        nodes.Add(new CommentNode(child.Text));
                        break;
                }
            }

            return nodes;
        }

        // Following an @if, consumes [else-if]/[else] element siblings into a nested else branch.
        private static TemplateNode BuildElseChain(IReadOnlyList<HtmlNode> children, ref int idx, HashSet<string> components, HashSet<string> directives)
        {
            var nextIdx = NextElementIndex(children, idx);
            if (nextIdx < 0)
            {
                return null;
            }

            var next = children[nextIdx];

            var elseIf = next.GetAttribute("[else-if]");
            if (elseIf != null)
            {
                idx = nextIdx;
                var body = BuildElement(next, components, directives);
                var chained = BuildElseChain(children, ref idx, components, directives);

                return new IfNode(ExpressionParser.Parse(elseIf), body, chained);
            }

            if (next.HasAttribute("[else]"))
            {
                idx = nextIdx;

                return BuildElement(next, components, directives);
            }

            return null;
        }

        private static int NextElementIndex(IReadOnlyList<HtmlNode> children, int from)
        {
            for (var k = from + 1; k < children.Count; k++)
            {
                if (children[k].NodeType == HtmlNodeType.Element)
                {
                    return k;
                }
            }

            return -1;
        }

        private static TemplateNode BuildComponent(HtmlNode element, string tagName)
        {
            var properties = new Dictionary<string, Expression>();

            foreach (var attribute in element.Attributes)
            {
                var name = attribute.Name.ToLowerInvariant();

                if (name.StartsWith("[") && name.EndsWith("]") && !name.Contains("."))
                {
                    var propertyName = name.Substring(1, name.Length - 2);

                    // Structural directives are handled by the wrapping/grouping in BuildElement, not
                    // as props — don't let them leak into a component property named If/For/Else/etc.
                    if (propertyName == "if" || propertyName == "for" || propertyName == "not-empty"
                        || propertyName == "else-if" || propertyName == "else")
                    {
                        continue;
                    }

                    properties[propertyName] = ExpressionParser.Parse(attribute.Value);
                }
            }

            return new ComponentNode(tagName, properties);
        }

        private static TemplateNode BuildElement(HtmlNode element, HashSet<string> components, HashSet<string> directives)
        {
            // AngleSharp lowercased tag names; preserve that so component/directive lookup and output
            // match regardless of how the template cased its tags.
            var tagName = element.Name.ToLowerInvariant();

            if (components.Contains(tagName))
            {
                // Structural directives apply to a component element too. Same wrapping order as regular
                // elements: [not-empty] innermost, then [if], then [for] outermost.
                TemplateNode componentNode = BuildComponent(element, tagName);

                var componentNotEmpty = element.GetAttribute("[not-empty]");
                if (componentNotEmpty != null)
                {
                    componentNode = new NotEmptyNode(ExpressionParser.Parse(componentNotEmpty), componentNode);
                }

                var componentIf = element.GetAttribute("[if]");
                if (componentIf != null)
                {
                    componentNode = new IfNode(ExpressionParser.Parse(componentIf), componentNode);
                }

                var componentFor = element.GetAttribute("[for]");
                if (componentFor != null)
                {
                    componentNode = new ForNode(ExpressionParser.Parse(componentFor), componentNode);
                }

                return componentNode;
            }

            string ifExpression = null;
            string forExpression = null;
            string notEmptyExpression = null;

            var attributes = new List<AttributeNode>();
            var bindings = new List<BindingNode>();
            var directiveBindings = new List<DirectiveBinding>();

            foreach (var attribute in element.Attributes)
            {
                // AngleSharp lowercased attribute names; match that so output and directive/binding
                // recognition are case-insensitive like HTML.
                var name = attribute.Name.ToLowerInvariant();

                if (name == "[if]")
                {
                    ifExpression = attribute.Value;
                    continue;
                }

                if (name == "[for]")
                {
                    forExpression = attribute.Value;
                    continue;
                }

                if (name == "[not-empty]")
                {
                    notEmptyExpression = attribute.Value;
                    continue;
                }

                // Structural markers consumed by the @if/@else grouping in ParseChildren.
                if (name == "[else-if]" || name == "[else]")
                {
                    continue;
                }

                if (name == "[html]")
                {
                    bindings.Add(new BindingNode(BindingKind.Html, null, ExpressionParser.Parse(attribute.Value)));
                    continue;
                }

                if (TryPrefixedBinding(name, "[attr.", BindingKind.Attribute, attribute.Value, bindings)) continue;
                if (TryPrefixedBinding(name, "[style.", BindingKind.Style, attribute.Value, bindings)) continue;
                if (TryPrefixedBinding(name, "[class.", BindingKind.Class, attribute.Value, bindings)) continue;

                if (name.StartsWith("[") && name.EndsWith("]") && !name.Contains("."))
                {
                    var bare = name.Substring(1, name.Length - 2);
                    if (directives.Contains(bare))
                    {
                        directiveBindings.Add(new DirectiveBinding(bare, ExpressionParser.Parse(attribute.Value)));
                        continue;
                    }
                }

                attributes.Add(new AttributeNode(name, attribute.Value));
            }

            TemplateNode node = new ElementNode(tagName, attributes, bindings, directiveBindings, ParseChildren(element.Children, components, directives));

            if (notEmptyExpression != null)
            {
                node = new NotEmptyNode(ExpressionParser.Parse(notEmptyExpression), node);
            }

            if (ifExpression != null)
            {
                node = new IfNode(ExpressionParser.Parse(ifExpression), node);
            }

            if (forExpression != null)
            {
                node = new ForNode(ExpressionParser.Parse(forExpression), node);
            }

            return node;
        }

        // Handles the "[prefix.target]" binding family ([attr.x] / [style.x] / [class.x]).
        private static bool TryPrefixedBinding(string name, string prefix, BindingKind kind, string value, List<BindingNode> bindings)
        {
            if (!name.StartsWith(prefix) || !name.EndsWith("]"))
            {
                return false;
            }

            var target = name.Substring(prefix.Length, name.Length - prefix.Length - 1);
            bindings.Add(new BindingNode(kind, target, ExpressionParser.Parse(value)));

            return true;
        }

        private static void AppendText(List<TemplateNode> nodes, string text)
        {
            var last = 0;

            foreach (Match match in InterpolationPattern.Matches(text))
            {
                if (match.Index > last)
                {
                    nodes.Add(new TextNode(text.Substring(last, match.Index - last)));
                }

                nodes.Add(new InterpolationNode(ExpressionParser.Parse(match.Groups[1].Value)));
                last = match.Index + match.Length;
            }

            if (last < text.Length)
            {
                nodes.Add(new TextNode(text.Substring(last)));
            }
        }
    }
}
