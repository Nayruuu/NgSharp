using System;
using System.Text.Json;
using System.Reflection;
using System.Globalization;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NgSharp;

// CLR-object ingestion via reflection — the only part of NgElement that is not trim / Native-AOT clean.
public readonly partial struct NgElement
{
    #region Fields

    private static readonly MethodInfo BoxBoolMethod =
        typeof(NgElement).GetMethod(nameof(BoxBool), BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly MethodInfo BoxLongMethod =
        typeof(NgElement).GetMethod(nameof(BoxLong), BindingFlags.NonPublic | BindingFlags.Static);

    // Immutable entries in a concurrent dictionary — safe under concurrent renders.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, BindableProperty[]> PropertyCache =
        new System.Collections.Concurrent.ConcurrentDictionary<Type, BindableProperty[]>();

    // Name-keyed view of the same metadata, for the lazy reader's by-name lookup.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Dictionary<string, BindableProperty>> PropertyMapCache =
        new System.Collections.Concurrent.ConcurrentDictionary<Type, Dictionary<string, BindableProperty>>();

    #endregion

    // Shapes whose normalization is baked into the compiled getter; None = fall through to TryScalar/recursion at runtime.
    private enum ScalarShape : byte { None, Bool, Number, String }

    private readonly struct BindableProperty
    {
        public readonly PropertyInfo Property;

        public readonly string Name;

        public readonly bool SkipWhenNull;

        public readonly ScalarShape Shape;

        // Returns the FINAL store value for its Shape (shared bool box / boxed long / boxed double / string), or (object)Prop for None.
        public readonly Func<object, object> Getter;

        public BindableProperty(PropertyInfo property, string name, bool skipWhenNull, ScalarShape shape, Func<object, object> getter)
        {
            Property = property;
            Name = name;
            SkipWhenNull = skipWhenNull;
            Shape = shape;
            Getter = getter;
        }
    }

    #region Public methods

    /// <summary>
    /// Builds an <see cref="NgElement"/> directly from a CLR object graph via reflection — the fastest
    /// path (opt-in via <see cref="HtmlBuilder.BuildFromTemplate(string, NgElement, TemplateOptions)"/>), skipping the
    /// model → JSON string → JsonDocument round-trip that <see cref="FromJson"/> needs.
    /// </summary>
    /// <remarks>
    /// Mirrors System.Text.Json's default mapping so it renders identically for the common cases
    /// (integral → long, float/double/decimal → double, DateTime/Guid/byte[] → string, enum → its
    /// number) and honors <c>[JsonPropertyName]</c> and <c>[JsonIgnore]</c>. NOT honored — use the
    /// object overload <see cref="HtmlBuilder.BuildFromTemplate(string, object, TemplateOptions)"/> for these:
    /// custom <c>[JsonConverter]</c>, <c>[JsonNumberHandling]</c>, naming policies. Object/array nodes
    /// keep <see cref="Value"/> null, so a pipe that re-deserializes a whole object from
    /// <c>value.Value</c> won't work with a FromObject-built model. Reflection-based, so this is the
    /// only part of <see cref="NgElement"/> that is not trim / Native-AOT clean.
    /// </remarks>
    /// <param name="value">The object graph to convert.</param>
    /// <returns>The root of the built model.</returns>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Walks the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Walks the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
#endif
    public static NgElement FromObject(object value)
        => MakeLazy(value);

    #endregion

    #region Private methods

    // Maps a non-null CLR scalar to (kind, boxed); false when the value needs recursion (IDictionary / IEnumerable / POCO).
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("May serialize a scalar via reflection-based System.Text.Json (float / DateTime).")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("May serialize a scalar via reflection-based System.Text.Json (float / DateTime).")]
#endif
    private static bool TryScalar(object value, out JsonValueKind kind, out object boxed)
    {
        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Boolean:
                kind = (bool)value ? JsonValueKind.True : JsonValueKind.False;
                boxed = value;

                return true;

            case TypeCode.String:
            case TypeCode.Char:
                kind = JsonValueKind.String;
                boxed = value.ToString();

                return true;

            // Enums land here too: GetTypeCode returns the underlying integral type, matching STJ's default enum-as-number.
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
                kind = JsonValueKind.Number;
                boxed = BoxLong(Convert.ToInt64(value, CultureInfo.InvariantCulture));

                return true;

            case TypeCode.UInt64:
                var unsignedValue = (ulong)value;
                kind = JsonValueKind.Number;
                boxed = unsignedValue <= long.MaxValue ? BoxLong((long)unsignedValue) : (double)unsignedValue;

                return true;

            case TypeCode.Single:
                // (double)float would expose the float's binary imprecision — go through STJ's shortest-round-trip text instead.
                kind = JsonValueKind.Number;
                boxed = double.Parse(JsonSerializer.Serialize(value, value.GetType()), CultureInfo.InvariantCulture);

                return true;

            case TypeCode.Double:
            case TypeCode.Decimal:
                kind = JsonValueKind.Number;
                boxed = Convert.ToDouble(value, CultureInfo.InvariantCulture);

                return true;

            case TypeCode.DateTime:
                kind = JsonValueKind.String;
                boxed = StjScalarString(value);

                return true;
        }

        switch (value)
        {
            case Guid guid:
                kind = JsonValueKind.String;
                boxed = guid.ToString();

                return true;

            case byte[] bytes:
                kind = JsonValueKind.String;
                boxed = Convert.ToBase64String(bytes);

                return true;

            case DateTimeOffset:
                kind = JsonValueKind.String;
                boxed = StjScalarString(value);

                return true;
        }

        kind = default;
        boxed = null;

        return false;
    }

    // Exact STJ text for a deferred scalar (DateTime(Offset), TimeSpan, Uri, …) without replicating
    // its formatting rules: serialize and strip the quotes. Any escape sequence (e.g. a Uri's
    // non-ASCII, \uXXXX-encoded by the default encoder) re-reads through JsonDocument so the text
    // stays byte-identical to the JSON ingestion path (which unescapes on parse).
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflection-based System.Text.Json serialization of a scalar.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Reflection-based System.Text.Json serialization of a scalar.")]
#endif
    private static string StjScalarString(object value)
    {
        var json = JsonSerializer.Serialize(value, value.GetType());

        if (json.Length >= 2 && json[0] == '"')
        {
            if (json.IndexOf('\\') < 0)
            {
                return json.Substring(1, json.Length - 2);
            }

            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.GetString();
        }

        return json;
    }

    // Always returns the shared box — the bool KIND is later recovered by reference identity (WrapMember / AddObjectNode).
    private static object BoxBool(bool value) => value ? BoxedTrue : BoxedFalse;

    // Only value-preserving, culture-independent normalizations are baked in; anything else stays None for TryScalar.
    private static ScalarShape ClassifyShape(Type type)
    {
        if (type == typeof(string))
        {
            return ScalarShape.String;
        }

        if (type == typeof(bool))
        {
            return ScalarShape.Bool;
        }

        if (type.IsEnum)
        {
            // enum → its underlying integral → long (matches TryScalar); ulong-backed enums keep the slow path.
            return Enum.GetUnderlyingType(type) == typeof(ulong) ? ScalarShape.None : ScalarShape.Number;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return ScalarShape.Number;
            default:
                return ScalarShape.None;
        }
    }

    private static bool IsIntegralNumber(Type type)
    {
        if (type.IsEnum)
        {
            return true;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
                return true;
            default:
                return false;   // Double / Decimal
        }
    }

    // For a bakeable Shape the compiled getter returns the FINAL store value, applying the same
    // conversions as TryScalar; for None it returns plain (object)Prop for runtime normalization.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Compiles a property accessor via System.Linq.Expressions. Use the JsonElement path (FromJson) for Native AOT.")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Compiles a property accessor via reflection.")]
#endif
    private static Func<object, object> CompileGetter(PropertyInfo property, ScalarShape shape)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var access = Expression.Property(Expression.Convert(instance, property.DeclaringType), property);

        Expression body;
        switch (shape)
        {
            case ScalarShape.Bool:
                body = Expression.Call(BoxBoolMethod, access);   // obj => BoxBool(((T)obj).Prop)
                break;

            case ScalarShape.Number:
                if (IsIntegralNumber(property.PropertyType))
                {
                    body = Expression.Call(BoxLongMethod, Expression.Convert(access, typeof(long)));   // obj => BoxLong((long)((T)obj).Prop)
                }
                else
                {
                    body = Expression.Convert(Expression.Convert(access, typeof(double)), typeof(object));   // (object)(double)Prop
                }

                break;

            default:
                body = Expression.Convert(access, typeof(object));   // None / String: (object)Prop
                break;
        }

        return Expression.Lambda<Func<object, object>>(body, instance).Compile();
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the type's public properties. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Compiles per-property accessors via System.Linq.Expressions. Use the JsonElement path (FromJson) for Native AOT.")]
#endif
    private static BindableProperty[] GetBindableProperties(Type type)
    {
        if (PropertyCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var built = BuildBindableProperties(type);

        return PropertyCache.GetOrAdd(type, built);
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the type's public properties.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Compiles per-property accessors via System.Linq.Expressions.")]
#endif
    private static Dictionary<string, BindableProperty> GetPropertyMap(Type type)
    {
        if (PropertyMapCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var props = GetBindableProperties(type);
        var map = new Dictionary<string, BindableProperty>(props.Length);

        foreach (var prop in props)
        {
            map[prop.Name] = prop;   // last-wins (property names are unique for a POCO)
        }

        return PropertyMapCache.GetOrAdd(type, map);
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the type's public properties. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Compiles per-property accessors via System.Linq.Expressions. Use the JsonElement path (FromJson) for Native AOT.")]
#endif
    private static BindableProperty[] BuildBindableProperties(Type type)
    {
        var list = new List<BindableProperty>();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.CanRead == false || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
            if (ignore is not null && ignore.Condition == JsonIgnoreCondition.Always)
            {
                continue;
            }

            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            var skipWhenNull = ignore is not null && ignore.Condition == JsonIgnoreCondition.WhenWritingNull;
            var shape = ClassifyShape(property.PropertyType);

            list.Add(new BindableProperty(property, name, skipWhenNull, shape, CompileGetter(property, shape)));
        }

        return list.ToArray();
    }

    #endregion
}
