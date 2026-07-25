using System;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Parsing;

// The fused parser's element layer: one open tag to its folded/dynamic form, plus the [else-if]/[else]
// chain that follows an @if and the [case]/[default] arm scan inside a [switch] container.
internal static partial class TemplateParser
{
    // Parses one element and either folds it into the emitter's const run (fully static — returns null)
    // or returns the built dynamic node WITHOUT emitting it (the caller chains [else] first, then emits).
    // tagStart/inChain feed validation only: tagStart is the '<' offset (-1 = unknown), inChain marks a
    // link consumed by ChainElseFused — outside a chain, [else]/[else-if] markers are dead and flagged.
    private static TemplateNode EmitElementFused(
        FoldEmitter emitter, string source, ref int pos, string rawName, List<HtmlAttribute> attrs, bool selfClosing,
        List<string> openNames, HashSet<string> components, HashSet<string> directives, int tagStart = -1, bool inChain = false)
    {
        if (inChain == false && attrs is not null && DiagnosticCollector.Current is { } collector)
        {
            var orphan = HasAttrRaw(attrs, "[else-if]") ? "[else-if]" : HasAttrRaw(attrs, "[else]") ? "[else]" : null;
            if (orphan is not null)
            {
                collector.ReportExpanded(DiagnosticSeverity.Error,
                    $"Orphan '{orphan}' — it must sit on the element immediately following an [if]/[else-if] sibling (or an @if/@else if block), otherwise the branch never renders.",
                    tagStart < 0 ? pos : tagStart);
            }

            var strayArm = HasAttrRaw(attrs, "[case]") ? "[case]" : HasAttrRaw(attrs, "[default]") ? "[default]" : null;
            if (strayArm is not null)
            {
                collector.ReportExpanded(DiagnosticSeverity.Error,
                    $"Orphan '{strayArm}' — it must sit on a direct child of a [switch] container (an @case/@default inside an @switch block); as written the marker is dropped and the element renders unconditionally.",
                    tagStart < 0 ? pos : tagStart);
            }
        }

        var tagName = rawName.ToLowerInvariant();
        var isVoid = selfClosing || HtmlVoidElements.Contains(rawName);

        // <ng-template #name>: the body folds like any program; the def node survives for @render.
        if (tagName == "ng-template")
        {
            var body = isVoid
                ? (IReadOnlyList<TemplateNode>)Array.Empty<TemplateNode>()
                : ParseScopedChildren(source, ref pos, rawName, openNames, components, directives);

            return new TemplateDefNode(TemplateRefNameF(attrs), body);
        }

        // @render marker: children (if any) are consumed and discarded.
        var renderTarget = GetAttrRaw(attrs, "[render]");
        if (renderTarget is not null)
        {
            if (isVoid == false)
            {
                ParseScopedChildren(source, ref pos, rawName, openNames, components, directives);
            }

            var renderContext = GetAttrRaw(attrs, "[render-context]");

            return new RenderTemplateNode(renderTarget, renderContext is null ? null : ExpressionParser.Parse(renderContext));
        }

        // @switch marker (<ng-container [switch]="E">): the transparent container's children are
        // scanned for [case]/[default] arms — see ScanSwitchChildrenFused.
        var switchExpression = GetAttrRaw(attrs, "[switch]");
        if (switchExpression is not null && tagName == "ng-container")
        {
            return BuildSwitchFused(source, ref pos, rawName, isVoid, switchExpression, openNames, components, directives);
        }

        if (components.Contains(tagName))
        {
            if (isVoid == false)
            {
                ParseScopedChildren(source, ref pos, rawName, openNames, components, directives);   // consumed, discarded
            }

            var componentNode = BuildComponentFused(attrs, tagName);

            var componentNotEmpty = GetAttrRaw(attrs, "[not-empty]");
            if (componentNotEmpty is not null)
            {
                componentNode = new NotEmptyNode(ExpressionParser.Parse(componentNotEmpty), componentNode);
            }

            var componentEmpty = GetAttrRaw(attrs, "[empty]");
            if (componentEmpty is not null)
            {
                componentNode = new EmptyNode(ExpressionParser.Parse(componentEmpty), componentNode);
            }

            var componentIf = GetAttrRaw(attrs, "[if]");
            if (componentIf is not null)
            {
                var componentCondition = ExpressionParser.Parse(componentIf);
                DiagnosticCollector.Current?.CheckIfCondition(componentCondition, componentIf, tagStart < 0 ? pos : tagStart);
                componentNode = new IfNode(componentCondition, componentNode);
            }

            var componentFor = GetAttrRaw(attrs, "[for]");
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

        if (attrs is not null)
        {
            for (var k = 0; k < attrs.Count; k++)
            {
                var attribute = attrs[k];
                var name = attribute.Name.ToLowerInvariant();

                if (name == "[if]") { ifExpression = attribute.Value; continue; }
                if (name == "[for]") { forExpression = attribute.Value; continue; }
                if (name == "[for-var]") { forVar = attribute.Value; continue; }
                if (name == "[not-empty]") { notEmptyExpression = attribute.Value; continue; }
                if (name == "[empty]") { emptyExpression = attribute.Value; continue; }
                if (name == "[else-if]" || name == "[else]" || name == "[case]" || name == "[default]") { continue; }

                if (name == "[html]")
                {
                    (bindings ??= new List<BindingNode>()).Add(new BindingNode(BindingKind.Html, null, ExpressionParser.Parse(attribute.Value)));
                    continue;
                }

                if (TryPrefixedBinding(name, "[attr.", BindingKind.Attribute, attribute.Value, ref bindings))
                {
                    continue;
                }

                if (TryPrefixedBinding(name, "[style.", BindingKind.Style, attribute.Value, ref bindings))
                {
                    continue;
                }

                if (TryPrefixedBinding(name, "[class.", BindingKind.Class, attribute.Value, ref bindings))
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

                    (bindings ??= new List<BindingNode>()).Add(new BindingNode(BindingKind.Attribute, bare, ExpressionParser.Parse(attribute.Value)));
                    continue;
                }

                (attributes ??= new List<AttributeNode>()).Add(new AttributeNode(name, attribute.Value));
            }
        }

        var hasStructural = ifExpression is not null || forExpression is not null || notEmptyExpression is not null || emptyExpression is not null;
        var hasBindings = bindings is not null || directiveBindings is not null;
        var isRawText = IsRawTextF(rawName);
        var isTransparent = tagName == "ng-container";

        TemplateNode inner;

        if (isTransparent && hasStructural == false && hasBindings == false)
        {
            // Plain ng-container: transparent — children inline into the CURRENT run.
            if (isVoid == false)
            {
                openNames.Add(rawName);
                EmitChildrenFused(emitter, source, ref pos, openNames, components, directives);
                openNames.RemoveAt(openNames.Count - 1);
            }

            return null;
        }

        if (isRawText)
        {
            // Rawtext (<script>/<style>) is opaque: children stay raw Text/Interpolation nodes — folding
            // them would wrongly route the text through the HTML escaper.
            var rawChildren = isVoid
                ? (IReadOnlyList<TemplateNode>)Array.Empty<TemplateNode>()
                : ReadRawTextChildrenFused(source, ref pos, rawName, openNames, components, directives);

            inner = new ElementNode(
                tagName,
                attributes ?? (IReadOnlyList<AttributeNode>)Array.Empty<AttributeNode>(),
                bindings ?? (IReadOnlyList<BindingNode>)Array.Empty<BindingNode>(),
                directiveBindings ?? (IReadOnlyList<DirectiveBinding>)Array.Empty<DirectiveBinding>(),
                rawChildren);
        }
        else if (isTransparent)
        {
            // ng-container with structural/bindings: folded children become the body (container bindings
            // are dropped).
            inner = SingleOrFragment(isVoid
                ? new List<TemplateNode>()
                : ParseScopedChildren(source, ref pos, rawName, openNames, components, directives));
        }
        else if (hasBindings == false)
        {
            // Fixed-attribute element: non-structural linearizes into the CURRENT run; structural into a
            // FRESH run that becomes the wrapped body. A self-closed non-void still closes
            // (<span/> -> <span></span>); only true void elements emit no close tag at all.
            var isTrueVoid = HtmlVoidElements.Contains(rawName);

            if (hasStructural == false)
            {
                AppendStaticOpenTagF(emitter.Const, tagName, attributes);
                if (isVoid == false)
                {
                    openNames.Add(rawName);
                    EmitChildrenFused(emitter, source, ref pos, openNames, components, directives);
                    openNames.RemoveAt(openNames.Count - 1);
                }

                if (isTrueVoid == false)
                {
                    emitter.Const.Append("</").Append(tagName).Append('>');
                }

                return null;
            }

            var bodyEmitter = new FoldEmitter();
            AppendStaticOpenTagF(bodyEmitter.Const, tagName, attributes);
            if (isVoid == false)
            {
                openNames.Add(rawName);
                EmitChildrenFused(bodyEmitter, source, ref pos, openNames, components, directives);
                openNames.RemoveAt(openNames.Count - 1);
            }

            if (isTrueVoid == false)
            {
                bodyEmitter.Const.Append("</").Append(tagName).Append('>');
            }

            inner = SingleOrFragment(bodyEmitter.Finish());
        }
        else
        {
            // Bound/directive element: opaque node, children folded into their own list.
            var children = isVoid
                ? (IReadOnlyList<TemplateNode>)Array.Empty<TemplateNode>()
                : ParseScopedChildren(source, ref pos, rawName, openNames, components, directives);

            inner = new ElementNode(
                tagName,
                attributes ?? (IReadOnlyList<AttributeNode>)Array.Empty<AttributeNode>(),
                bindings ?? (IReadOnlyList<BindingNode>)Array.Empty<BindingNode>(),
                directiveBindings ?? (IReadOnlyList<DirectiveBinding>)Array.Empty<DirectiveBinding>(),
                children);
        }

        if (notEmptyExpression is not null)
        {
            inner = new NotEmptyNode(ExpressionParser.Parse(notEmptyExpression), inner);
        }

        if (emptyExpression is not null)
        {
            inner = new EmptyNode(ExpressionParser.Parse(emptyExpression), inner);
        }

        if (ifExpression is not null)
        {
            var condition = ExpressionParser.Parse(ifExpression);
            DiagnosticCollector.Current?.CheckIfCondition(condition, ifExpression, tagStart < 0 ? pos : tagStart);
            inner = new IfNode(condition, inner);
        }

        if (forExpression is not null)
        {
            inner = new ForNode(ExpressionParser.Parse(forExpression), forVar, inner);
        }

        return inner;
    }

    private static List<TemplateNode> ParseScopedChildren(string source, ref int pos, string rawName, List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        var emitter = new FoldEmitter();

        openNames.Add(rawName);
        EmitChildrenFused(emitter, source, ref pos, openNames, components, directives);
        openNames.RemoveAt(openNames.Count - 1);

        return emitter.Finish();
    }

    // A control-flow body collapses to its single instruction, else a FragmentNode — CompileBody's contract.
    private static TemplateNode SingleOrFragment(List<TemplateNode> nodes)
        => nodes.Count == 1 ? nodes[0] : new FragmentNode(nodes);

    // <script>/<style>: literal text up to the close-tag PREFIX, then parsing resumes with lenient
    // close recovery.
    private static IReadOnlyList<TemplateNode> ReadRawTextChildrenFused(string source, ref int pos, string rawName, List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        var childList = new List<TemplateNode>();
        var rawEnd = IndexOfIgnoreCaseF(source, "</" + rawName, pos);
        var stop = rawEnd < 0 ? source.Length : rawEnd;

        AppendText(childList, source.Substring(pos, stop - pos), pos);
        pos = stop;

        // Pathological continuation (a mismatched close ended the raw run early): text must stay RAW
        // TextNodes — RenderRawChildren drops everything but Text/Interpolation, so folding to ConstNodes
        // here would silently lose content.
        openNames.Add(rawName);
        while (pos < source.Length)
        {
            if (source[pos] == '<' && IsMarkupStartF(source, pos))
            {
                if (StartsWithF(source, pos, "<!--"))
                {
                    var start = pos + 4;
                    var end = source.IndexOf("-->", start, StringComparison.Ordinal);

                    childList.Add(new CommentNode(end < 0 ? source.Substring(start) : source.Substring(start, end - start)));
                    pos = end < 0 ? source.Length : end + 3;
                    continue;
                }

                if (source[pos + 1] == '!')
                {
                    var end = source.IndexOf('>', pos);
                    pos = end < 0 ? source.Length : end + 1;
                    continue;
                }

                if (source[pos + 1] == '/')
                {
                    ScanCloseTagF(source, pos, out var nameStart, out var nameLen, out var after);

                    if (SpanNameEqualsF(source, nameStart, nameLen, rawName))
                    {
                        pos = after;
                        break;
                    }

                    if (StackContainsSpanF(openNames, source, nameStart, nameLen, openNames.Count - 1))
                    {
                        break;          // implicit close by an ancestor
                    }

                    pos = after;        // stray close: ignored
                    continue;
                }

                // Nested tag: built normally (the raw renderer drops non-Text/Interpolation children).
                var nestedStart = pos;
                ReadTagHeaderF(source, ref pos, out var nestedName, out var nestedAttrs, out var nestedSelfClosing);
                var nestedEmitter = new FoldEmitter();
                var nested = EmitElementFused(nestedEmitter, source, ref pos, nestedName, nestedAttrs, nestedSelfClosing, openNames, components, directives, nestedStart);
                if (nested is not null)
                {
                    nestedEmitter.Emit(nested);
                }

                childList.AddRange(nestedEmitter.Finish());
                continue;
            }

            var textStart = pos;
            while (pos < source.Length && (source[pos] == '<' && IsMarkupStartF(source, pos)) == false)
            {
                pos++;
            }

            AppendText(childList, source.Substring(textStart, pos - textStart), textStart);
        }

        openNames.RemoveAt(openNames.Count - 1);

        return childList;
    }

    // Following an @if: scans past interstitial text/comments/declarations/stray-closes to the next
    // element sibling; consumes it into the chain when it carries [else-if]/[else], else rewinds.
    private static TemplateNode ChainElseFused(string source, ref int pos, List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        var save = pos;
        var scan = pos;

        while (scan < source.Length)
        {
            if (source[scan] == '<' && IsMarkupStartF(source, scan))
            {
                if (StartsWithF(source, scan, "<!--"))
                {
                    var end = source.IndexOf("-->", scan + 4, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        break;
                    }

                    scan = end + 3;
                    continue;
                }

                if (source[scan + 1] == '!')
                {
                    var end = source.IndexOf('>', scan);
                    if (end < 0)
                    {
                        break;
                    }

                    scan = end + 1;
                    continue;
                }

                if (source[scan + 1] == '/')
                {
                    ScanCloseTagF(source, scan, out var nameStart, out var nameLen, out var after);
                    if (StackContainsSpanF(openNames, source, nameStart, nameLen, openNames.Count))
                    {
                        break;   // this close ends the current children list — chain over
                    }

                    scan = after;
                    continue;
                }

                var headerPos = scan;
                ReadTagHeaderF(source, ref headerPos, out var nextName, out var nextAttrs, out var nextSelfClosing);

                var elseIf = GetAttrRaw(nextAttrs, "[else-if]");
                if (elseIf is not null)
                {
                    pos = headerPos;
                    var body = BuildChainBody(source, ref pos, nextName, nextAttrs, nextSelfClosing, openNames, components, directives);
                    var chained = ChainElseFused(source, ref pos, openNames, components, directives);
                    var condition = ExpressionParser.Parse(elseIf);
                    DiagnosticCollector.Current?.CheckIfCondition(condition, elseIf, scan);

                    return new IfNode(condition, body, chained);
                }

                if (HasAttrRaw(nextAttrs, "[else]"))
                {
                    pos = headerPos;

                    return BuildChainBody(source, ref pos, nextName, nextAttrs, nextSelfClosing, openNames, components, directives);
                }

                break;
            }

            scan++;
            while (scan < source.Length && (source[scan] == '<' && IsMarkupStartF(source, scan)) == false)
            {
                scan++;
            }
        }

        pos = save;

        return null;
    }

    // A chain link parsed in BODY position: folds into a fresh run and collapses (CompileBody semantics).
    private static TemplateNode BuildChainBody(string source, ref int pos, string rawName, List<HtmlAttribute> attrs, bool selfClosing, List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        var emitter = new FoldEmitter();
        var node = EmitElementFused(emitter, source, ref pos, rawName, attrs, selfClosing, openNames, components, directives, inChain: true);
        if (node is not null)
        {
            emitter.Emit(node);
        }

        return SingleOrFragment(emitter.Finish());
    }

    // The @switch marker's node: value parsed once, arms collected from the container's children.
    private static TemplateNode BuildSwitchFused(
        string source, ref int pos, string rawName, bool isVoid, string switchExpression,
        List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        var cases = new List<SwitchCase>();
        TemplateNode defaultBody = null;

        if (isVoid == false)
        {
            openNames.Add(rawName);
            ScanSwitchChildrenFused(source, ref pos, openNames, components, directives, cases, ref defaultBody);
            openNames.RemoveAt(openNames.Count - 1);
        }

        return new SwitchNode(ExpressionParser.Parse(switchExpression), cases, defaultBody);
    }

    // The [switch] container's child scan: only [case]/[default] arms and whitespace are legal —
    // anything else is consumed, DROPPED from the program, and (validation only) reported as stray.
    // Same close-tag recovery contract as EmitChildrenFused.
    private static void ScanSwitchChildrenFused(
        string source, ref int pos, List<string> openNames, HashSet<string> components, HashSet<string> directives,
        List<SwitchCase> cases, ref TemplateNode defaultBody)
    {
        while (pos < source.Length)
        {
            if (source[pos] == '<' && IsMarkupStartF(source, pos))
            {
                if (StartsWithF(source, pos, "<!--"))
                {
                    ReportStraySwitchContent(pos);
                    var end = source.IndexOf("-->", pos + 4, StringComparison.Ordinal);
                    pos = end < 0 ? source.Length : end + 3;
                    continue;
                }

                if (source[pos + 1] == '!')
                {
                    ReportStraySwitchContent(pos);
                    var end = source.IndexOf('>', pos);
                    pos = end < 0 ? source.Length : end + 1;
                    continue;
                }

                if (source[pos + 1] == '/')
                {
                    ScanCloseTagF(source, pos, out var nameStart, out var nameLen, out var after);

                    if (openNames.Count > 0 && SpanNameEqualsF(source, nameStart, nameLen, openNames[openNames.Count - 1]))
                    {
                        pos = after;

                        return;
                    }

                    if (StackContainsSpanF(openNames, source, nameStart, nameLen, openNames.Count - 1))
                    {
                        return;         // implicit close — leave the tag unconsumed for the matching ancestor
                    }

                    pos = after;        // stray close: ignored
                    continue;
                }

                var tagStart = pos;
                ReadTagHeaderF(source, ref pos, out var armName, out var armAttrs, out var armSelfClosing);

                var caseExpression = GetAttrRaw(armAttrs, "[case]");
                if (caseExpression is not null)
                {
                    cases.Add(new SwitchCase(
                        ExpressionParser.Parse(caseExpression),
                        BuildChainBody(source, ref pos, armName, armAttrs, armSelfClosing, openNames, components, directives)));
                    continue;
                }

                if (HasAttrRaw(armAttrs, "[default]"))
                {
                    var body = BuildChainBody(source, ref pos, armName, armAttrs, armSelfClosing, openNames, components, directives);
                    defaultBody ??= body;   // first @default wins
                    continue;
                }

                // Stray element: consumed whole (an @if's else chain included) and dropped.
                ReportStraySwitchContent(tagStart);
                var throwaway = new FoldEmitter();
                var stray = EmitElementFused(throwaway, source, ref pos, armName, armAttrs, armSelfClosing, openNames, components, directives, tagStart);
                if (stray is IfNode)
                {
                    ChainElseFused(source, ref pos, openNames, components, directives);
                }

                throwaway.Finish();
                continue;
            }

            var textStart = pos;
            while (pos < source.Length && (source[pos] == '<' && IsMarkupStartF(source, pos)) == false)
            {
                pos++;
            }

            ReportStraySwitchText(source, textStart, pos);
        }
    }

    // Both stray reporters are validation-only (Current is null on every plain parse) — a lenient
    // render silently drops the same content.
    private static void ReportStraySwitchContent(int expandedPosition)
        => DiagnosticCollector.Current?.ReportExpanded(DiagnosticSeverity.Error,
            "Stray content inside '@switch' — only '@case (…)' / '@default' blocks and whitespace are allowed there; this content never renders.", expandedPosition);

    private static void ReportStraySwitchText(string source, int start, int end)
    {
        if (DiagnosticCollector.Current is null)
        {
            return;
        }

        for (var k = start; k < end; k++)
        {
            if (char.IsWhiteSpace(source[k]) == false)
            {
                ReportStraySwitchContent(k);

                return;
            }
        }
    }

    private static TemplateNode BuildComponentFused(List<HtmlAttribute> attrs, string tagName)
    {
        var properties = new Dictionary<string, Expression>();

        if (attrs is not null)
        {
            for (var k = 0; k < attrs.Count; k++)
            {
                var name = attrs[k].Name.ToLowerInvariant();

                if (name.StartsWith("[") && name.EndsWith("]") && name.Contains(".") == false)
                {
                    var propertyName = name.Substring(1, name.Length - 2);

                    if (propertyName == "if" || propertyName == "for" || propertyName == "not-empty"
                        || propertyName == "empty" || propertyName == "else-if" || propertyName == "else"
                        || propertyName == "case" || propertyName == "default")
                    {
                        continue;
                    }

                    properties[propertyName] = ExpressionParser.Parse(attrs[k].Value);
                }
            }
        }

        return new ComponentNode(tagName, properties);
    }

    private static string GetAttrRaw(List<HtmlAttribute> attrs, string name)
    {
        if (attrs is null)
        {
            return null;
        }

        for (var k = 0; k < attrs.Count; k++)
        {
            if (attrs[k].Name == name)
            {
                return attrs[k].Value;
            }
        }

        return null;
    }

    private static bool HasAttrRaw(List<HtmlAttribute> attrs, string name) => GetAttrRaw(attrs, name) is not null;

    private static string TemplateRefNameF(List<HtmlAttribute> attrs)
    {
        if (attrs is null)
        {
            return null;
        }

        for (var k = 0; k < attrs.Count; k++)
        {
            if (attrs[k].Name.Length > 1 && attrs[k].Name[0] == '#')
            {
                return attrs[k].Name.Substring(1);
            }
        }

        return null;
    }
}
