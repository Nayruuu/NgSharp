using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;
using NgSharp.Expressions;

namespace NgSharp.Template
{
    public static class TemplateRenderer
    {
        // HTML void elements: serialized as a single tag, no children, no closing tag.
        private static readonly HashSet<string> VoidElements = new HashSet<string>
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr"
        };

        // Rawtext elements: their text content is CSS/JS, not HTML — it must NOT be entity-escaped
        // (the browser never decodes entities there, so escaping would corrupt e.g. a `>` combinator).
        private static readonly HashSet<string> RawTextElements = new HashSet<string> { "style", "script" };

        public static string Render(
            IReadOnlyList<TemplateNode> nodes,
            NgElement context,
            IReadOnlyDictionary<string, IPipe> pipes = null,
            IReadOnlyDictionary<string, IComponent> components = null,
            IReadOnlyDictionary<string, IDirective> directives = null)
        {
            var builder = new StringBuilder();

            RenderNodes(nodes, context, new RenderScope(pipes, components, directives), builder);

            return builder.ToString();
        }

        // Bundles the constant registries so they don't have to be threaded one by one.
        private sealed class RenderScope
        {
            public IReadOnlyDictionary<string, IPipe> Pipes { get; }

            public IReadOnlyDictionary<string, IComponent> Components { get; }

            public IReadOnlyDictionary<string, IDirective> Directives { get; }

            public RenderScope(IReadOnlyDictionary<string, IPipe> pipes, IReadOnlyDictionary<string, IComponent> components, IReadOnlyDictionary<string, IDirective> directives)
            {
                Pipes = pipes;
                Components = components;
                Directives = directives;
            }
        }

        private static void RenderNodes(IReadOnlyList<TemplateNode> nodes, NgElement context, RenderScope scope, StringBuilder builder)
        {
            foreach (var node in nodes)
            {
                RenderNode(node, context, scope, builder);
            }
        }

        private static void RenderNode(TemplateNode node, NgElement context, RenderScope scope, StringBuilder builder)
        {
            switch (node)
            {
                case TextNode text:
                    builder.Append(EscapeText(text.Text));
                    break;

                case CommentNode comment:
                    builder.Append("<!--").Append(comment.Text).Append("-->");
                    break;

                case InterpolationNode interpolation:
                    var value = ExpressionEvaluator.Evaluate(interpolation.Expression, context, scope.Pipes);
                    builder.Append(Escape(value?.Value?.ToString()));
                    break;

                case ElementNode element:
                    RenderElement(element, context, scope, builder);
                    break;

                case IfNode ifNode:
                    var condition = ExpressionEvaluator.Evaluate(ifNode.Condition, context, scope.Pipes);
                    if (condition.GetBoolean() ?? false)
                    {
                        RenderNode(ifNode.Body, context, scope, builder);
                    }
                    else if (ifNode.Else != null)
                    {
                        RenderNode(ifNode.Else, context, scope, builder);
                    }
                    break;

                case ForNode forNode:
                    var collection = ExpressionEvaluator.Evaluate(forNode.Collection, context, scope.Pipes);
                    foreach (var item in collection.Children)
                    {
                        RenderNode(forNode.Body, item, scope, builder);
                    }
                    break;

                case NotEmptyNode notEmptyNode:
                    var candidate = ExpressionEvaluator.Evaluate(notEmptyNode.Collection, context, scope.Pipes);
                    if (candidate.Children.Count > 0)
                    {
                        RenderNode(notEmptyNode.Body, context, scope, builder);
                    }
                    break;

                case ComponentNode component:
                    RenderComponent(component, context, scope, builder);
                    break;
            }
        }

        private static void RenderElement(ElementNode element, NgElement context, RenderScope scope, StringBuilder builder)
        {
            // Transparent wrapper produced by the @if/@for preprocessor: render children only.
            if (element.TagName == "ng-container")
            {
                RenderNodes(element.Children, context, scope, builder);
                return;
            }

            // Attribute values are escaped as they enter the list (static = entity-aware, keeping
            // authored entities; dynamic binding data = full escape) so class/style merges concatenate
            // already-escaped parts; WriteElement then emits them verbatim.
            var attributes = new List<KeyValuePair<string, string>>();
            foreach (var attribute in element.Attributes)
            {
                attributes.Add(new KeyValuePair<string, string>(attribute.Name, EscapeAttributeText(attribute.Value)));
            }

            var innerHtml = ApplyBindings(element, context, scope, attributes);
            ApplyDirectives(element, context, scope, attributes);

            WriteElement(element, attributes, innerHtml, context, scope, builder);
        }

        // Runs each custom [directive] against the element's attribute list before it is written.
        private static void ApplyDirectives(ElementNode element, NgElement context, RenderScope scope, List<KeyValuePair<string, string>> attributes)
        {
            if (element.Directives == null || element.Directives.Count == 0 || scope.Directives == null)
            {
                return;
            }

            DirectiveElement target = null;
            foreach (var directive in element.Directives)
            {
                if (scope.Directives.TryGetValue(directive.Name, out var impl))
                {
                    target = target ?? new DirectiveElement(element.TagName, attributes);
                    var content = ExpressionEvaluator.Evaluate(directive.Expression, context, scope.Pipes, element.TagName);
                    impl.Apply(target, content);
                }
            }
        }

        // Applies [attr.x] / [style.x] / [class.x] / [html] to the attribute list; returns the
        // raw inner HTML from an [html] binding, or null.
        private static string ApplyBindings(ElementNode element, NgElement context, RenderScope scope, List<KeyValuePair<string, string>> attributes)
        {
            string innerHtml = null;

            foreach (var binding in element.Bindings)
            {
                var result = ExpressionEvaluator.Evaluate(binding.Expression, context, scope.Pipes, element.TagName);
                var value = result?.Value?.ToString();

                switch (binding.Kind)
                {
                    case BindingKind.Html:
                        if (result != null && result.ValueKind == JsonValueKind.String)
                        {
                            // [html] injects the raw markup verbatim (an innerHTML-style trusted assignment).
                            innerHtml = result.GetString();
                        }
                        break;

                    case BindingKind.Attribute when binding.Target == "class":
                        if (!string.IsNullOrEmpty(value))
                        {
                            AppendToAttribute(attributes, "class", " ", EscapeAttribute(value));
                        }
                        break;

                    case BindingKind.Attribute:
                        // A null value (missing data) omits the attribute; an empty string still sets it.
                        if (value != null)
                        {
                            SetAttribute(attributes, binding.Target, EscapeAttribute(value));
                        }
                        break;

                    case BindingKind.Style:
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            AppendToAttribute(attributes, "style", "; ", $"{binding.Target}: {EscapeAttribute(value)}");
                        }
                        break;

                    case BindingKind.Class:
                        // [class.name]="expr" toggles the class when the expression is truthy.
                        if (result?.GetBoolean() ?? false)
                        {
                            AppendToAttribute(attributes, "class", " ", binding.Target);
                        }
                        break;
                }
            }

            return innerHtml;
        }

        private static void WriteElement(ElementNode element, List<KeyValuePair<string, string>> attributes, string innerHtml, NgElement context, RenderScope scope, StringBuilder output)
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
            else if (innerHtml != null)
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
        private static void RenderRawChildren(IReadOnlyList<TemplateNode> children, NgElement context, RenderScope scope, StringBuilder output)
        {
            foreach (var child in children)
            {
                switch (child)
                {
                    case TextNode text:
                        output.Append(text.Text);
                        break;

                    case InterpolationNode interpolation:
                        var value = ExpressionEvaluator.Evaluate(interpolation.Expression, context, scope.Pipes);
                        output.Append(value?.Value?.ToString());
                        break;
                }
            }
        }

        private static void RenderComponent(ComponentNode component, NgElement context, RenderScope scope, StringBuilder builder)
        {
            if (scope.Components == null || !scope.Components.TryGetValue(component.Name, out var registered))
            {
                return;
            }

            // A fresh instance per render keeps the shared registry entry immutable (thread-safe).
            var instance = (IComponent)Activator.CreateInstance(registered.GetType());
            var properties = instance.GetType().GetProperties();

            foreach (var property in component.Properties)
            {
                var target = properties.FirstOrDefault(p => string.Equals(p.Name, property.Key, StringComparison.OrdinalIgnoreCase));
                if (target == null || !target.CanWrite)
                {
                    continue;
                }

                var evaluated = ExpressionEvaluator.Evaluate(property.Value, context, scope.Pipes);
                var converted = ConvertValue(evaluated, target.PropertyType);
                if (converted != null)
                {
                    target.SetValue(instance, converted);
                }
            }

            builder.Append(instance.Render());
        }

        private static object ConvertValue(NgElement value, Type targetType)
        {
            if (value == null || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                if (type == typeof(string)) return value.GetString();
                if (type == typeof(int)) return value.GetInt();
                if (type == typeof(long)) return value.GetLong();
                if (type == typeof(float)) return value.GetFloat();
                if (type == typeof(double)) return value.GetDouble();
                if (type == typeof(decimal)) return value.GetDecimal();
                if (type == typeof(bool)) return value.GetBoolean();
                if (type == typeof(DateTime)) return value.GetDateTime();
                if (type == typeof(Guid)) return Guid.Parse(value.GetString());
                // System.Text.Json serializes byte[] as base64, so decode it back.
                if (type == typeof(byte[])) return Convert.FromBase64String(value.GetString());
                if (type.IsEnum) return Enum.Parse(type, value.GetString(), ignoreCase: true);

                // Complex component-property types (collections, POCOs) beyond the ones handled above
                // are not bound — reflection-based deserialization here would break trimming / Native AOT.
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static void SetAttribute(List<KeyValuePair<string, string>> attributes, string name, string value)
        {
            for (int i = 0; i < attributes.Count; i++)
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
            for (int i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].Key == name)
                {
                    attributes[i] = new KeyValuePair<string, string>(name, attributes[i].Value + separator + addition);
                    return;
                }
            }

            attributes.Add(new KeyValuePair<string, string>(name, addition));
        }

        // Full escaping for interpolated *data* \u2014 every & becomes &amp; (the value is not markup).
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\u00A0", "&nbsp;");
        }

        // Entity references already written in the template (&nbsp;, &amp;, &eacute;, &#233;) are
        // authored markup \u2014 preserve them; only a bare & (and </>) is escaped. Static template text
        // is trusted markup, unlike interpolated data which goes through Escape.
        private static readonly Regex BareAmpersand = new Regex(@"&(?!#\d+;|#x[0-9a-fA-F]+;|[a-zA-Z][a-zA-Z0-9]*;)", RegexOptions.Compiled);

        private static string EscapeText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return BareAmpersand.Replace(value, "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\u00A0", "&nbsp;");
        }

        // Full escaping for a dynamically-bound (data) attribute value.
        private static string EscapeAttribute(string value)
        {
            return Escape(value).Replace("\"", "&quot;");
        }

        // Static (authored) attribute value: preserve entities already written in the template
        // (&amp;, &eacute;), escape only a bare & and the quote delimiter.
        private static string EscapeAttributeText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return BareAmpersand.Replace(value, "&amp;").Replace("\"", "&quot;");
        }
    }
}
