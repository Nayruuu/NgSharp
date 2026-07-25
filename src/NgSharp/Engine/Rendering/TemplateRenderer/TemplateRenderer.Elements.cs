using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Rendering;

// The renderer's element layer: static fast path, bound slow path, binding application and rawtext children.
internal static partial class TemplateRenderer
{
    #region Fields

    // HTML void elements: serialized as a single tag, no children, no closing tag.
    private static readonly HashSet<string> VoidElements = new HashSet<string>
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    // Rawtext elements: content is CSS/JS, never entity-escaped (the browser doesn't decode entities there).
    private static readonly HashSet<string> RawTextElements = new HashSet<string> { "style", "script" };

    #endregion

    #region Private methods

    private static void RenderElement(ElementNode element, NgElement context, RenderScope scope, PooledCharWriter builder)
    {
        // Transparent wrapper produced by the @if/@for preprocessor: render children only.
        if (element.TagName == "ng-container")
        {
            RenderNodes(element.Children, context, scope, builder);

            return;
        }

        // Fast path: no bindings and no directives means a fixed attribute set — write it straight out.
        var hasBindings = element.Bindings.Count > 0;
        var hasDirectives = element.Directives is not null && element.Directives.Count > 0 && scope.Directives is not null;

        if (hasBindings == false && hasDirectives == false)
        {
            WriteStaticElement(element, context, scope, builder);

            return;
        }

        // Slow path: values are escaped as they ENTER the list (static = entity-aware, dynamic = full
        // escape) so class/style merges concatenate already-escaped parts; WriteElement emits them verbatim.
        List<KeyValuePair<string, string>> attributes;
        if (hasDirectives)
        {
            // A custom [directive] receives the live list via DirectiveElement (it may retain the
            // reference), so it must own a fresh list — never the shared per-render scratch buffer.
            attributes = new List<KeyValuePair<string, string>>(element.Attributes.Count);
        }
        else
        {
            attributes = scope.ScratchAttributes;
            attributes.Clear();
        }

        for (var i = 0; i < element.Attributes.Count; i++)
        {
            var attribute = element.Attributes[i];
            attributes.Add(new KeyValuePair<string, string>(attribute.Name, HtmlEscaper.EscapeAttributeText(attribute.Value)));
        }

        var innerHtml = ApplyBindings(element, context, scope, attributes);
        ApplyDirectives(element, context, scope, attributes);

        WriteElement(element, attributes, innerHtml, context, scope, builder);
    }

    // Static fast path — must produce byte-identical output to the slow path for such elements.
    private static void WriteStaticElement(ElementNode element, NgElement context, RenderScope scope, PooledCharWriter output)
    {
        AppendStaticOpenTag(output, element);

        if (VoidElements.Contains(element.TagName))
        {
            return;
        }

        if (RawTextElements.Contains(element.TagName))
        {
            RenderRawChildren(element.Children, context, scope, output);
        }
        else
        {
            RenderNodes(element.Children, context, scope, output);
        }

        AppendStaticCloseTag(output, element);
    }

    // Applies the bindings to the attribute list; returns the raw inner HTML from an [html] binding, or null.
    private static string ApplyBindings(ElementNode element, NgElement context, RenderScope scope, List<KeyValuePair<string, string>> attributes)
    {
        string innerHtml = null;

        for (var i = 0; i < element.Bindings.Count; i++)
        {
            var binding = element.Bindings[i];
            var result = ExpressionEvaluator.Evaluate(binding.Expression, scope.ScopeChain, scope.Pipes, element.TagName);
            var value = result.Value?.ToString();

            switch (binding.Kind)
            {
                case BindingKind.Html:
                    if (result.ValueKind == JsonValueKind.String)
                    {
                        // [html] injects the raw markup verbatim (an innerHTML-style trusted assignment).
                        innerHtml = result.GetString();
                    }

                    break;

                case BindingKind.Attribute when binding.Target == "class":
                    if (string.IsNullOrEmpty(value) == false)
                    {
                        AppendToAttribute(attributes, "class", " ", HtmlEscaper.EscapeAttribute(value));
                    }

                    break;

                case BindingKind.Attribute:
                    // A null value (missing data) omits the attribute; an empty string still sets it.
                    if (value is not null)
                    {
                        SetAttribute(attributes, binding.Target, HtmlEscaper.EscapeAttribute(value));
                    }

                    break;

                case BindingKind.Style:
                    if (string.IsNullOrWhiteSpace(value) == false)
                    {
                        AppendToAttribute(attributes, "style", "; ", $"{binding.Target}: {HtmlEscaper.EscapeAttribute(value)}");
                    }

                    break;

                case BindingKind.Class:
                    // [class.name]="expr" toggles the class when the expression is truthy.
                    if (result.GetBoolean() ?? false)
                    {
                        AppendToAttribute(attributes, "class", " ", binding.Target);
                    }

                    break;
            }
        }

        return innerHtml;
    }

    private static void WriteElement(ElementNode element, List<KeyValuePair<string, string>> attributes, string innerHtml, NgElement context, RenderScope scope, PooledCharWriter output)
    {
        output.Append('<').Append(element.TagName);
        foreach (var attribute in attributes)
        {
            output.Append(' ').Append(attribute.Key).Append("=\"").Append(attribute.Value).Append('"');
        }

        output.Append('>');

        if (VoidElements.Contains(element.TagName))
        {
            return;
        }

        if (RawTextElements.Contains(element.TagName))
        {
            RenderRawChildren(element.Children, context, scope, output);
        }
        else if (innerHtml is not null)
        {
            output.Append(innerHtml);
        }
        else
        {
            RenderNodes(element.Children, context, scope, output);
        }

        output.Append("</").Append(element.TagName).Append('>');
    }

    // Text/interpolation inside a rawtext element (<style>/<script>) — emitted without escaping.
    private static void RenderRawChildren(IReadOnlyList<TemplateNode> children, NgElement context, RenderScope scope, PooledCharWriter output)
    {
        for (var i = 0; i < children.Count; i++)
        {
            switch (children[i])
            {
                case TextNode text:
                    output.Append(text.Text);
                    break;

                case InterpolationNode interpolation:
                    var value = ExpressionEvaluator.Evaluate(interpolation.Expression, scope.ScopeChain, scope.Pipes);
                    output.Append(value.Value?.ToString());

                    break;
            }
        }
    }

    private static void SetAttribute(List<KeyValuePair<string, string>> attributes, string name, string value)
    {
        for (var i = 0; i < attributes.Count; i++)
        {
            if (attributes[i].Key == name)
            {
                attributes[i] = new KeyValuePair<string, string>(name, value);

                return;
            }
        }

        attributes.Add(new KeyValuePair<string, string>(name, value));
    }

    private static void AppendToAttribute(List<KeyValuePair<string, string>> attributes, string name, string separator, string addition)
    {
        for (var i = 0; i < attributes.Count; i++)
        {
            if (attributes[i].Key == name)
            {
                attributes[i] = new KeyValuePair<string, string>(name, attributes[i].Value + separator + addition);

                return;
            }
        }

        attributes.Add(new KeyValuePair<string, string>(name, addition));
    }

    #endregion

    #region Internal methods

    internal static void AppendStaticOpenTag(PooledCharWriter output, ElementNode element)
    {
        output.Append('<').Append(element.TagName);
        var staticAttributes = element.Attributes;
        for (var i = 0; i < staticAttributes.Count; i++)
        {
            var attribute = staticAttributes[i];
            output.Append(' ').Append(attribute.Name).Append("=\"").Append(HtmlEscaper.EscapeAttributeText(attribute.Value)).Append('"');
        }

        output.Append('>');
    }

    internal static void AppendStaticCloseTag(PooledCharWriter output, ElementNode element)
        => output.Append("</").Append(element.TagName).Append('>');

    #endregion
}
