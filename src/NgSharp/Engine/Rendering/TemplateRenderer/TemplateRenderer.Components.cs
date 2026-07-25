using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Rendering;

// The renderer's extensibility layer: components, custom [directive]s, <ng-template> collection + @render outlets.
internal static partial class TemplateRenderer
{
    private static void CollectTemplates(IReadOnlyList<TemplateNode> nodes, ref Dictionary<string, IReadOnlyList<TemplateNode>> map)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            CollectTemplates(nodes[i], ref map);
        }
    }

    private static void CollectTemplates(TemplateNode node, ref Dictionary<string, IReadOnlyList<TemplateNode>> map)
    {
        switch (node)
        {
            case TemplateDefNode def:
                if (def.Name is not null)
                {
                    map ??= new Dictionary<string, IReadOnlyList<TemplateNode>>();
                    map[def.Name] = def.Body;   // last definition of a name wins
                }

                CollectTemplates(def.Body, ref map);

                break;

            case ElementNode element:
                CollectTemplates(element.Children, ref map);
                break;

            case FragmentNode fragment:
                CollectTemplates(fragment.Nodes, ref map);
                break;

            case IfNode ifNode:
                CollectTemplates(ifNode.Body, ref map);
                if (ifNode.Else is not null)
                {
                    CollectTemplates(ifNode.Else, ref map);
                }

                break;

            case ForNode forNode:
                CollectTemplates(forNode.Body, ref map);
                break;

            case NotEmptyNode notEmpty:
                CollectTemplates(notEmpty.Body, ref map);
                break;

            case EmptyNode empty:
                CollectTemplates(empty.Body, ref map);
                break;

            case SwitchNode switchNode:
                for (var i = 0; i < switchNode.Cases.Count; i++)
                {
                    CollectTemplates(switchNode.Cases[i].Body, ref map);
                }

                if (switchNode.Default is not null)
                {
                    CollectTemplates(switchNode.Default, ref map);
                }

                break;
        }
    }

    // @render(name[, ctx]): silently renders nothing for an unknown name; a depth cap stops runaway recursion.
    private static void RenderTemplateOutlet(RenderTemplateNode render, NgElement context, RenderScope scope, PooledCharWriter builder)
    {
        if (scope.Templates is null || scope.Templates.TryGetValue(render.Name, out var body) == false)
        {
            return;
        }

        if (scope.EnterTemplate())
        {
            if (render.Context is null)
            {
                RenderNodes(body, context, scope, builder);
            }
            else
            {
                // Explicit context: the fragment renders against ONLY that context — no outer scope.
                var templateContext = ExpressionEvaluator.Evaluate(render.Context, scope.ScopeChain, scope.Pipes);
                var saved = scope.EnterIsolatedScope(templateContext);
                RenderNodes(body, templateContext, scope, builder);
                scope.ExitIsolatedScope(saved);
            }
        }

        scope.ExitTemplate();
    }

    private static void ApplyDirectives(ElementNode element, NgElement context, RenderScope scope, List<KeyValuePair<string, string>> attributes)
    {
        if (element.Directives is null || element.Directives.Count == 0 || scope.Directives is null)
        {
            return;
        }

        DirectiveElement target = null;
        for (var i = 0; i < element.Directives.Count; i++)
        {
            var directive = element.Directives[i];
            if (scope.Directives.TryGetValue(directive.Name, out var implementation))
            {
                target ??= new DirectiveElement(element.TagName, attributes);
                var content = ExpressionEvaluator.Evaluate(directive.Expression, scope.ScopeChain, scope.Pipes, element.TagName);
                implementation.Apply(target, content);
            }
        }
    }

    private static void RenderComponent(ComponentNode component, NgElement context, RenderScope scope, PooledCharWriter builder)
    {
        if (scope.Components is null || scope.Components.TryGetValue(component.Name, out var registered) == false)
        {
            return;
        }

        // A fresh instance per render keeps the shared registry entry immutable (thread-safe); the
        // registration's annotated Type keeps activation and property binding trim-safe.
        var instance = (IComponent)Activator.CreateInstance(registered.Type);
        var properties = registered.Type.GetProperties();

        foreach (var property in component.Properties)
        {
            var target = properties.FirstOrDefault(p => string.Equals(p.Name, property.Key, StringComparison.OrdinalIgnoreCase));
            if (target is null || target.CanWrite == false)
            {
                continue;
            }

            var evaluated = ExpressionEvaluator.Evaluate(property.Value, scope.ScopeChain, scope.Pipes);
            var converted = ConvertValue(evaluated, target.PropertyType, scope, component.Name, target.Name);
            if (converted is not null)
            {
                target.SetValue(instance, converted);
            }
        }

        // The component's output is trusted raw HTML by contract (see IComponent) — appended verbatim,
        // like [html]; escaping user data inside the markup is the component's responsibility.
        builder.Append(instance.Render());
    }

    // Complex-type conversions (live CLR carrier as-is, or JSON deserialization) share the reflection
    // caveat already documented on component binding for trimming / Native AOT.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Component property binding is documented as reflection-based under trimming / Native AOT (see the HtmlBuilder.BuildFromTemplate(string, JsonElement) remarks): the bound property types must be preserved by the app.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Component property binding is documented as reflection-based under trimming / Native AOT (see the HtmlBuilder.BuildFromTemplate(string, JsonElement) remarks): the bound property types must be preserved by the app.")]
#endif
    private static object ConvertValue(NgElement value, Type targetType, RenderScope scope, string componentName, string propertyName)
    {
        if (value.IsUndefined || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (type == typeof(string))
            {
                return value.GetString();
            }

            if (type == typeof(int))
            {
                return value.GetInt();
            }

            if (type == typeof(long))
            {
                return value.GetLong();
            }

            if (type == typeof(float))
            {
                return value.GetFloat();
            }

            if (type == typeof(double))
            {
                return value.GetDouble();
            }

            if (type == typeof(decimal))
            {
                return value.GetDecimal();
            }

            if (type == typeof(bool))
            {
                return value.GetBoolean();
            }

            if (type == typeof(DateTime))
            {
                return value.GetDateTime();
            }

            if (type == typeof(Guid))
            {
                return Guid.Parse(value.GetString());
            }

            // System.Text.Json serializes byte[] as base64, so decode it back.
            if (type == typeof(byte[]))
            {
                return Convert.FromBase64String(value.GetString());
            }

            if (type.IsEnum)
            {
                return Enum.Parse(type, value.GetString(), ignoreCase: true);
            }

            // Head of the complex cascade: a node whose RAW carrier already IS the target type binds
            // as-is — deferred scalar boxes (DateTimeOffset, TimeSpan, Uri, …) carry String kind, so
            // the hosted-CLR check below (Object/Array kinds only) would refuse them.
            var carrier = value.CarrierForBinding;
            if (carrier is not null && type.IsInstanceOfType(carrier))
            {
                return carrier;
            }

            // Complex property types (collections, POCOs): a FromObject model carries the ORIGINAL
            // CLR value — bind it as-is when assignable; a FromJson node deserializes to the target.
            if (value.TryGetHostedClrValue(out var hosted))
            {
                return targetType.IsInstanceOfType(hosted) ? hosted : null;
            }

            if (value.TryGetJsonElement(out var element))
            {
                return JsonSerializer.Deserialize(element, targetType);
            }

            return null;
        }
        catch (Exception exception)
        {
            // Non-strict keeps the documented lenient contract: a failed conversion leaves the
            // property null. A strict render must fail loudly instead, naming the culprit.
            if (scope.ScopeChain.Strict)
            {
                ThrowComponentConversionFailed(componentName, propertyName, targetType, exception);
            }

            return null;
        }
    }

    // Out of line so the strict test in ConvertValue's catch stays a compare + a never-taken jump.
    private static void ThrowComponentConversionFailed(string componentName, string propertyName, Type targetType, Exception exception)
        => throw new NgSharpException(
            $"Strict mode: component '{componentName}' property '{propertyName}': conversion to {targetType.Name} failed — {exception.Message}",
            exception);

    // Returns null when the tree has no <ng-template> fragments (the common case).
    internal static IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> CollectTemplates(IReadOnlyList<TemplateNode> nodes)
    {
        Dictionary<string, IReadOnlyList<TemplateNode>> map = null;
        CollectTemplates(nodes, ref map);

        return map;
    }
}
