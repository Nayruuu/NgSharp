using System;
using System.Text.Json;
using System.Reflection;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NgSharp
{
    // Builds the NgElement tree directly from a CLR object graph via reflection (the opt-in fast path).
    // Reflection-based, so it is the only part of NgElement that is not trim / Native-AOT clean — kept in
    // its own file and marked accordingly.
    public partial class NgElement
    {
        /// <summary>
        /// Builds an <see cref="NgElement"/> tree directly from a CLR object graph via reflection — the
        /// fastest path (opt-in via <see cref="HtmlBuilder.BuildFromTemplateAsync(string, NgElement)"/>),
        /// skipping the model → JSON string → JsonDocument round-trip that <see cref="FromJson"/> needs.
        /// </summary>
        /// <remarks>
        /// Mirrors System.Text.Json's default mapping so it renders identically for the common cases
        /// (integral → long, float/double/decimal → double, DateTime/Guid/byte[] → string, enum → its
        /// number) and honors <c>[JsonPropertyName]</c> and <c>[JsonIgnore]</c>. NOT honored — use the
        /// object overload <see cref="HtmlBuilder.BuildFromTemplateAsync(string, object)"/> for these:
        /// custom <c>[JsonConverter]</c>, <c>[JsonNumberHandling]</c>, naming policies. Object/array nodes
        /// keep <see cref="Value"/> null, so a pipe that re-deserializes a whole object from
        /// <c>value.Value</c> won't work with a FromObject-built tree. Reflection-based, so this is the
        /// only part of <see cref="NgElement"/> that is not trim / Native-AOT clean.
        /// </remarks>
        /// <param name="value">The object graph to convert.</param>
        /// <param name="parent">The parent node (null at the root; supplied during recursion).</param>
        /// <param name="key">This node's key (empty at the root; supplied during recursion).</param>
        /// <returns>The root of the built tree.</returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Walks the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Walks the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
#endif
        public static NgElement FromObject(object value, NgElement parent = null, string key = "")
            => FromObject(value, parent, key, 0);

#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Walks the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Walks the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
#endif
        private static NgElement FromObject(object value, NgElement parent, string key, int depth)
        {
            // Matches System.Text.Json's default MaxDepth — turns an object cycle into a clean error
            // instead of an uncatchable StackOverflow.
            if (depth > 64)
            {
                throw new InvalidOperationException(
                    "NgElement.FromObject exceeded 64 levels of nesting (possible object cycle). Break the cycle (e.g. [JsonIgnore]) or use the object overload of BuildFromTemplateAsync.");
            }

            var ng = new NgElement { Key = key, Parent = parent };

            if (value == null)
            {
                ng.ValueKind = JsonValueKind.Null;
                return ng;
            }

            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Boolean:
                    ng.ValueKind = (bool)value ? JsonValueKind.True : JsonValueKind.False;
                    ng.Value = value;
                    return ng;

                case TypeCode.String:
                case TypeCode.Char:
                    ng.ValueKind = JsonValueKind.String;
                    ng.Value = value.ToString();
                    return ng;

                // Enums land here too — GetTypeCode returns their underlying integral type, and STJ
                // serializes enums as that number by default, so the mapping already matches.
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                    ng.ValueKind = JsonValueKind.Number;
                    ng.Value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    return ng;

                case TypeCode.UInt64:
                    var u = (ulong)value;
                    ng.ValueKind = JsonValueKind.Number;
                    ng.Value = u <= long.MaxValue ? (object)(long)u : (double)u;
                    return ng;

                case TypeCode.Single:
                    // Widening a float with (double) exposes its binary imprecision; go through STJ's
                    // own shortest-round-trip text (what the JSON path produced) so the double matches.
                    ng.ValueKind = JsonValueKind.Number;
                    ng.Value = double.Parse(JsonSerializer.Serialize(value, value.GetType()), CultureInfo.InvariantCulture);
                    return ng;

                case TypeCode.Double:
                case TypeCode.Decimal:
                    ng.ValueKind = JsonValueKind.Number;
                    ng.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return ng;

                case TypeCode.DateTime:
                    ng.ValueKind = JsonValueKind.String;
                    ng.Value = StjScalarString(value);
                    return ng;
            }

            switch (value)
            {
                case Guid guid:
                    ng.ValueKind = JsonValueKind.String;
                    ng.Value = guid.ToString();
                    return ng;

                case byte[] bytes:
                    ng.ValueKind = JsonValueKind.String;
                    ng.Value = Convert.ToBase64String(bytes);
                    return ng;

                case DateTimeOffset:
                    ng.ValueKind = JsonValueKind.String;
                    ng.Value = StjScalarString(value);
                    return ng;

                case IDictionary dictionary:
                    ng.ValueKind = JsonValueKind.Object;
                    var entries = new NgElement[dictionary.Count];
                    var entryCount = 0;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        var name = entry.Key.ToString();
                        entries[entryCount++] = FromObject(entry.Value, ng, name, depth + 1);
                    }
                    ng.members = entries;
                    return ng;

                case IEnumerable enumerable:
                    ng.ValueKind = JsonValueKind.Array;
                    ng.children = new List<NgElement>();
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        ng.children.Add(FromObject(item, ng, $"[{index++}]", depth + 1));
                    }
                    return ng;

                default:
                    // Per-type property metadata is resolved once and cached; each render only pays the
                    // per-instance GetValue(), not the GetProperties + attribute lookups again.
                    var bindables = GetBindableProperties(value.GetType());
                    ng.ValueKind = JsonValueKind.Object;
                    var props = new NgElement[bindables.Length];
                    var propCount = 0;
                    foreach (var bindable in bindables)
                    {
                        var propValue = bindable.Property.GetValue(value);

                        if (bindable.SkipWhenNull && propValue == null)
                        {
                            continue;
                        }

                        props[propCount++] = FromObject(propValue, ng, bindable.Name, depth + 1);
                    }

                    // Trim only when a WhenWritingNull property was actually skipped (usually none).
                    ng.members = propCount == props.Length ? props : Trim(props, propCount);
                    return ng;
            }
        }

        private static NgElement[] Trim(NgElement[] array, int count)
        {
            var trimmed = new NgElement[count];
            Array.Copy(array, trimmed, count);
            return trimmed;
        }

        // Exact System.Text.Json scalar text for DateTime/DateTimeOffset without replicating STJ's
        // ISO-8601 formatting rules: serialize the single value and strip the surrounding quotes.
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflection-based System.Text.Json serialization of a scalar.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Reflection-based System.Text.Json serialization of a scalar.")]
#endif
        private static string StjScalarString(object value)
        {
            var json = JsonSerializer.Serialize(value, value.GetType());
            return json.Length >= 2 && json[0] == '"' ? json.Substring(1, json.Length - 2) : json;
        }

        // A bindable property with its render-time-invariant metadata resolved once: the accessor, the
        // JSON name ([JsonPropertyName] or the property name), and whether [JsonIgnore(WhenWritingNull)]
        // applies. [JsonIgnore(Always)] properties are dropped from the set entirely.
        private readonly struct BindableProperty
        {
            public readonly PropertyInfo Property;
            public readonly string Name;
            public readonly bool SkipWhenNull;

            public BindableProperty(PropertyInfo property, string name, bool skipWhenNull)
            {
                Property = property;
                Name = name;
                SkipWhenNull = skipWhenNull;
            }
        }

        // Reflection metadata never changes for a given type — resolve it once and reuse across renders.
        // Thread-safe: entries are immutable arrays and the dictionary is concurrent.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, BindableProperty[]> PropertyCache =
            new System.Collections.Concurrent.ConcurrentDictionary<Type, BindableProperty[]>();

#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the type's public properties. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
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
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the type's public properties. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
#endif
        private static BindableProperty[] BuildBindableProperties(Type type)
        {
            var list = new List<BindableProperty>();

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
                if (ignore != null && ignore.Condition == JsonIgnoreCondition.Always)
                {
                    continue;
                }

                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                var skipWhenNull = ignore != null && ignore.Condition == JsonIgnoreCondition.WhenWritingNull;

                list.Add(new BindableProperty(property, name, skipWhenNull));
            }

            return list.ToArray();
        }
    }
}
