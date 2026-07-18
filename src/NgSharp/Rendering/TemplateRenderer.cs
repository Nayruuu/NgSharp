using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Rendering
{
    internal static class TemplateRenderer
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
            // Pre-sized to skip the earliest tiny growth chunks; the builder grows past this as needed.
            var builder = new StringBuilder(256);

            RenderNodes(nodes, context, new RenderScope(pipes, components, directives), builder);

            return builder.ToString();
        }

        private static void RenderNodes(IReadOnlyList<TemplateNode> nodes, NgElement context, RenderScope scope, StringBuilder builder)
        {
            // Indexed loop, not foreach: iterating an IReadOnlyList<T> with foreach boxes an enumerator
            // on the heap every call, which across a large tree is a real allocation cost. The indexer
            // allocates nothing. (Same reasoning applies to the other IReadOnlyList loops below.)
            for (var i = 0; i < nodes.Count; i++)
            {
                RenderNode(nodes[i], context, scope, builder);
            }
        }

        // Appends an interpolated value. Numbers and booleans go straight into the builder through its
        // span-based Append overloads (TryFormat under the hood) — no intermediate ToString() string, and
        // they never contain characters that need escaping. Strings take the escaping path.
        private static void AppendValue(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    break;
                case string s:
                    builder.Append(HtmlEscaper.Escape(s));
                    break;
                case long l:
                    builder.Append(l);
                    break;
                case int i:
                    builder.Append(i);
                    break;
                case double d:
                    builder.Append(d);
                    break;
                case decimal m:
                    builder.Append(m);
                    break;
                case bool b:
                    builder.Append(b);
                    break;
                default:
                    builder.Append(HtmlEscaper.Escape(value.ToString()));
                    break;
            }
        }

        private static void RenderNode(TemplateNode node, NgElement context, RenderScope scope, StringBuilder builder)
        {
            switch (node)
            {
                case ConstNode constant:
                    // Folded static run (produced by TemplateProgram): already escaped, appended verbatim.
                    builder.Append(constant.Text);
                    break;

                case TextNode text:
                    builder.Append(HtmlEscaper.EscapeText(text.Text));
                    break;

                case CommentNode comment:
                    builder.Append("<!--").Append(comment.Text).Append("-->");
                    break;

                case FragmentNode fragment:
                    // Compiled control-flow body: render its instructions in order.
                    RenderNodes(fragment.Nodes, context, scope, builder);
                    break;

                case InterpolationNode interpolation:
                    var value = ExpressionEvaluator.Evaluate(interpolation.Expression, context, scope.Pipes);
                    AppendValue(builder, value?.Value);
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
                    var items = collection.Children;
                    for (var i = 0; i < items.Count; i++)
                    {
                        RenderNode(forNode.Body, items[i], scope, builder);
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

            // Fast path: with no [attr.x]/[class.x]/[style.x]/[html] bindings and no custom directives,
            // the attribute set is fixed — write the static attributes straight to the output and skip
            // the per-element List allocation entirely (the common case: static markup with plain attrs).
            var hasBindings = element.Bindings.Count > 0;
            var hasDirectives = element.Directives != null && element.Directives.Count > 0 && scope.Directives != null;

            if (!hasBindings && !hasDirectives)
            {
                WriteStaticElement(element, context, scope, builder);
                return;
            }

            // Slow path: bindings/directives can add, merge or mutate attributes, so materialize the
            // list. Values are escaped as they enter it (static = entity-aware, keeping authored
            // entities; dynamic binding data = full escape) so class/style merges concatenate
            // already-escaped parts; WriteElement then emits them verbatim.
            var attributes = new List<KeyValuePair<string, string>>(element.Attributes.Count);
            for (var i = 0; i < element.Attributes.Count; i++)
            {
                var attribute = element.Attributes[i];
                attributes.Add(new KeyValuePair<string, string>(attribute.Name, HtmlEscaper.EscapeAttributeText(attribute.Value)));
            }

            var innerHtml = ApplyBindings(element, context, scope, attributes);
            ApplyDirectives(element, context, scope, attributes);

            WriteElement(element, attributes, innerHtml, context, scope, builder);
        }

        // Writes an element whose attributes are fixed (no bindings/directives) directly to the output,
        // without allocating an intermediate attribute list. Produces byte-identical output to the
        // slow path for such elements.
        private static void WriteStaticElement(ElementNode element, NgElement context, RenderScope scope, StringBuilder output)
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

        internal static bool IsVoidElement(string tagName) => VoidElements.Contains(tagName);

        internal static bool IsRawTextElement(string tagName) => RawTextElements.Contains(tagName);

        // Emits the fixed opening tag of an element with no bindings/directives — shared by the static
        // render fast-path and TemplateProgram's compile-time folding, so both stay byte-identical.
        internal static void AppendStaticOpenTag(StringBuilder output, ElementNode element)
        {
            output.Append('<').Append(element.TagName);
            var staticAttrs = element.Attributes;
            for (var i = 0; i < staticAttrs.Count; i++)
            {
                var attribute = staticAttrs[i];
                output.Append(' ').Append(attribute.Name).Append("=\"").Append(HtmlEscaper.EscapeAttributeText(attribute.Value)).Append('"');
            }
            output.Append('>');
        }

        internal static void AppendStaticCloseTag(StringBuilder output, ElementNode element)
            => output.Append("</").Append(element.TagName).Append('>');

        // Runs each custom [directive] against the element's attribute list before it is written.
        private static void ApplyDirectives(ElementNode element, NgElement context, RenderScope scope, List<KeyValuePair<string, string>> attributes)
        {
            if (element.Directives == null || element.Directives.Count == 0 || scope.Directives == null)
            {
                return;
            }

            DirectiveElement target = null;
            for (var i = 0; i < element.Directives.Count; i++)
            {
                var directive = element.Directives[i];
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

            for (var i = 0; i < element.Bindings.Count; i++)
            {
                var binding = element.Bindings[i];
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
                            AppendToAttribute(attributes, "class", " ", HtmlEscaper.EscapeAttribute(value));
                        }
                        break;

                    case BindingKind.Attribute:
                        // A null value (missing data) omits the attribute; an empty string still sets it.
                        if (value != null)
                        {
                            SetAttribute(attributes, binding.Target, HtmlEscaper.EscapeAttribute(value));
                        }
                        break;

                    case BindingKind.Style:
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            AppendToAttribute(attributes, "style", "; ", $"{binding.Target}: {HtmlEscaper.EscapeAttribute(value)}");
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
            for (var i = 0; i < children.Count; i++)
            {
                switch (children[i])
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
    }
}
