// Reference pipeline (tests only): the original staged template walk (lexer -> tree-builder ->
// tree walk), moved out of the shipped assembly and kept HERE as the REFERENCE implementation the
// fused parser is differential-tested against (FusedParserDifferentialTests). InternalsVisibleTo
// gives it the lib types it shares: ControlFlowPreprocessor, ExpressionParser, the AST nodes, and
// TemplateParser.AppendText / TryPrefixedBinding — sharing those keeps both walks rule-identical
// where they must be (text splitting, binding prefixes), so the oracle only diverges on what it is
// meant to check: the HTML structure semantics.
#nullable disable
using System;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Parsing;

internal static class StagedTemplateParser
{
    #region Public methods

    public static IReadOnlyList<TemplateNode> ParseRootsViaStagedPipeline(string html, IEnumerable<string> componentNames = null, IEnumerable<string> directiveNames = null)
    {
        var expanded = ControlFlowPreprocessor.Expand(html ?? string.Empty);
        var roots = HtmlTreeBuilder.Build(HtmlLexer.Tokenize(expanded));

        return ParseChildren(roots, Set(componentNames), Set(directiveNames));
    }

    #endregion

    #region Private methods

    private static HashSet<string> Set(IEnumerable<string> names)
        => names is null ? new HashSet<string>() : new HashSet<string>(names);

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

                    // Fold following [else-if]/[else] siblings into the IfNode (idx advances past them).
                    if (node is IfNode ifNode)
                    {
                        var elseBranch = BuildElseChain(children, ref idx, components, directives);
                        if (elseBranch is not null)
                        {
                            node = new IfNode(ifNode.Condition, ifNode.Body, elseBranch);
                        }
                    }

                    nodes.Add(node);

                    break;

                case HtmlNodeType.Text:
                    TemplateParser.AppendText(nodes, child.Text);
                    break;

                case HtmlNodeType.Comment:
                    nodes.Add(new CommentNode(child.Text));
                    break;
            }
        }

        return nodes;
    }

    private static TemplateNode BuildElseChain(IReadOnlyList<HtmlNode> children, ref int idx, HashSet<string> components, HashSet<string> directives)
    {
        var nextIdx = NextElementIndex(children, idx);
        if (nextIdx < 0)
        {
            return null;
        }

        var next = children[nextIdx];

        var elseIf = next.GetAttribute("[else-if]");
        if (elseIf is not null)
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

    // The staged twin of BuildSwitchFused/ScanSwitchChildrenFused: arms from the tree's children,
    // first @default wins, everything that is no arm (stray text, comments, other elements) drops.
    private static TemplateNode BuildSwitch(HtmlNode element, string switchExpression, HashSet<string> components, HashSet<string> directives)
    {
        var cases = new List<SwitchCase>();
        TemplateNode defaultBody = null;

        foreach (var child in element.Children)
        {
            if (child.NodeType != HtmlNodeType.Element)
            {
                continue;
            }

            var caseExpression = child.GetAttribute("[case]");
            if (caseExpression is not null)
            {
                cases.Add(new SwitchCase(ExpressionParser.Parse(caseExpression), BuildElement(child, components, directives)));
                continue;
            }

            if (child.HasAttribute("[default]"))
            {
                defaultBody ??= BuildElement(child, components, directives);
            }
        }

        return new SwitchNode(ExpressionParser.Parse(switchExpression), cases, defaultBody);
    }

    private static TemplateNode BuildComponent(HtmlNode element, string tagName)
    {
        var properties = new Dictionary<string, Expression>();

        foreach (var attribute in element.Attributes)
        {
            var name = attribute.Name.ToLowerInvariant();

            if (name.StartsWith("[") && name.EndsWith("]") && name.Contains(".") == false)
            {
                var propertyName = name.Substring(1, name.Length - 2);

                // Structural directives are consumed by BuildElement's wrapping — never leak them as component props.
                if (propertyName == "if" || propertyName == "for" || propertyName == "not-empty"
                    || propertyName == "empty" || propertyName == "else-if" || propertyName == "else"
                    || propertyName == "case" || propertyName == "default")
                {
                    continue;
                }

                properties[propertyName] = ExpressionParser.Parse(attribute.Value);
            }
        }

        return new ComponentNode(tagName, properties);
    }

    // First '#'-prefixed attribute, case-preserved (matched against @render(name)); null when unnamed.
    private static string TemplateRefName(HtmlNode element)
    {
        foreach (var attribute in element.Attributes)
        {
            if (attribute.Name.Length > 1 && attribute.Name[0] == '#')
            {
                return attribute.Name.Substring(1);
            }
        }

        return null;
    }

    private static TemplateNode BuildElement(HtmlNode element, HashSet<string> components, HashSet<string> directives)
    {
        var tagName = element.Name.ToLowerInvariant();

        if (tagName == "ng-template")
        {
            return new TemplateDefNode(TemplateRefName(element), ParseChildren(element.Children, components, directives));
        }

        // [render]/[render-context] are the preprocessor's desugaring of @render(name[, ctx]).
        var renderTarget = element.GetAttribute("[render]");
        if (renderTarget is not null)
        {
            var renderContext = element.GetAttribute("[render-context]");

            return new RenderTemplateNode(renderTarget, renderContext is null ? null : ExpressionParser.Parse(renderContext));
        }

        // @switch marker (<ng-container [switch]="E">): the container's children are the arms — same
        // rule as the fused parser (only [case]/[default] children survive; everything else drops).
        var switchExpression = element.GetAttribute("[switch]");
        if (switchExpression is not null && tagName == "ng-container")
        {
            return BuildSwitch(element, switchExpression, components, directives);
        }

        if (components.Contains(tagName))
        {
            // Wrapping order (same as regular elements): [not-empty] innermost, then [if], then [for] outermost.
            var componentNode = BuildComponent(element, tagName);

            var componentNotEmpty = element.GetAttribute("[not-empty]");
            if (componentNotEmpty is not null)
            {
                componentNode = new NotEmptyNode(ExpressionParser.Parse(componentNotEmpty), componentNode);
            }

            var componentEmpty = element.GetAttribute("[empty]");
            if (componentEmpty is not null)
            {
                componentNode = new EmptyNode(ExpressionParser.Parse(componentEmpty), componentNode);
            }

            var componentIf = element.GetAttribute("[if]");
            if (componentIf is not null)
            {
                componentNode = new IfNode(ExpressionParser.Parse(componentIf), componentNode);
            }

            var componentFor = element.GetAttribute("[for]");
            if (componentFor is not null)
            {
                componentNode = new ForNode(ExpressionParser.Parse(componentFor), null, componentNode);
            }

            return componentNode;
        }

        string ifExpression = null;
        string forExpression = null;
        string forVar = null;
        string notEmptyExpression = null;
        string emptyExpression = null;

        List<AttributeNode> attributes = null;
        List<BindingNode> bindings = null;
        List<DirectiveBinding> directiveBindings = null;

        foreach (var attribute in element.Attributes)
        {
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

            if (name == "[for-var]")
            {
                // The explicit loop variable from @for (Var of Collection) — becomes a named scope frame.
                forVar = attribute.Value;
                continue;
            }

            if (name == "[not-empty]")
            {
                notEmptyExpression = attribute.Value;
                continue;
            }

            if (name == "[empty]")
            {
                emptyExpression = attribute.Value;
                continue;
            }

            // Structural markers consumed by the @if/@else grouping in ParseChildren and the
            // [switch] arm scan in BuildSwitch.
            if (name == "[else-if]" || name == "[else]" || name == "[case]" || name == "[default]")
            {
                continue;
            }

            if (name == "[html]")
            {
                (bindings ??= new List<BindingNode>()).Add(new BindingNode(BindingKind.Html, null, ExpressionParser.Parse(attribute.Value)));
                continue;
            }

            if (TemplateParser.TryPrefixedBinding(name, "[attr.", BindingKind.Attribute, attribute.Value, ref bindings))
            {
                continue;
            }

            if (TemplateParser.TryPrefixedBinding(name, "[style.", BindingKind.Style, attribute.Value, ref bindings))
            {
                continue;
            }

            if (TemplateParser.TryPrefixedBinding(name, "[class.", BindingKind.Class, attribute.Value, ref bindings))
            {
                continue;
            }

            if (name.StartsWith("[") && name.EndsWith("]") && name.Contains(".") == false)
            {
                var bare = name.Substring(1, name.Length - 2);
                if (directives.Contains(bare))
                {
                    (directiveBindings ??= new List<DirectiveBinding>()).Add(new DirectiveBinding(bare, ExpressionParser.Parse(attribute.Value)));
                    continue;
                }

                // A bare '[prop]' that is no registered directive is an Angular property binding — bind it
                // like '[attr.prop]' rather than leaking the literal '[prop]="..."' into the output.
                (bindings ??= new List<BindingNode>()).Add(new BindingNode(BindingKind.Attribute, bare, ExpressionParser.Parse(attribute.Value)));
                continue;
            }

            (attributes ??= new List<AttributeNode>()).Add(new AttributeNode(name, attribute.Value));
        }

        TemplateNode node = new ElementNode(
            tagName,
            attributes ?? (IReadOnlyList<AttributeNode>)Array.Empty<AttributeNode>(),
            bindings ?? (IReadOnlyList<BindingNode>)Array.Empty<BindingNode>(),
            directiveBindings ?? (IReadOnlyList<DirectiveBinding>)Array.Empty<DirectiveBinding>(),
            ParseChildren(element.Children, components, directives));

        if (notEmptyExpression is not null)
        {
            node = new NotEmptyNode(ExpressionParser.Parse(notEmptyExpression), node);
        }

        if (emptyExpression is not null)
        {
            node = new EmptyNode(ExpressionParser.Parse(emptyExpression), node);
        }

        if (ifExpression is not null)
        {
            node = new IfNode(ExpressionParser.Parse(ifExpression), node);
        }

        if (forExpression is not null)
        {
            node = new ForNode(ExpressionParser.Parse(forExpression), forVar, node);
        }

        return node;
    }

    #endregion
}
