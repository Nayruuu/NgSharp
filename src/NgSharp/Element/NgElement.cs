using System;
using System.Text.Json;
using System.Reflection;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace NgSharp
{
    public class NgElement
    {
        public string Key { get; set; }

        public object Value { get; private set; }

        public NgElement Parent { get; private set; }

        public JsonValueKind ValueKind { get; private set; }

        // Array items / object properties. A scalar node allocates neither — it shares a static empty
        // instance — so the getters never return null. The tree is read-only from the outside; build it
        // via FromJson / FromObject.
        private List<NgElement> children;

        private Dictionary<string, NgElement> properties;

        private static readonly IReadOnlyList<NgElement> EmptyChildren = Array.Empty<NgElement>();

        private static readonly IReadOnlyDictionary<string, NgElement> EmptyProperties =
            new ReadOnlyDictionary<string, NgElement>(new Dictionary<string, NgElement>());

        // Shared immutable boolean/null results. The evaluator returns these for comparison / logical /
        // null-path results instead of allocating a fresh NgElement each render. Safe to share: they are
        // leaf values (no Parent, no children) and are never mutated.
        internal static readonly NgElement True = new NgElement { Key = "", Value = true, ValueKind = JsonValueKind.True };

        internal static readonly NgElement False = new NgElement { Key = "", Value = false, ValueKind = JsonValueKind.False };

        internal static readonly NgElement Null = new NgElement { Key = "", Value = null, ValueKind = JsonValueKind.Null };

        public IReadOnlyList<NgElement> Children => this.children ?? EmptyChildren;

        public IReadOnlyDictionary<string, NgElement> Properties => this.properties ?? EmptyProperties;

        public string Path
        {
            get
            {
                if (Parent == null)
                    return Key;

                if (Key.StartsWith("["))
                    return $"{Parent.Path}{Key}";

                return string.IsNullOrEmpty(Parent.Path)
                    ? Key
                    : $"{Parent.Path}.{Key}";
            }
        }

        public string GetString()
        {
            return Value?.ToString();
        }

        public bool? GetBoolean() => Value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var r) => r,
            _ => null
        };

        public DateTime? GetDateTime() => Value switch
        {
            DateTime dt => dt,
            string s when DateTime.TryParse(s, out var r) => r,
            _ => null
        };

        // Value only ever holds long/double (from JSON) or int/decimal (from Parse) for numbers,
        // never float — so the numeric getters have no float case.
        public int? GetInt() => Value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal dec => (int)dec,
            string s when int.TryParse(s, out var r) => r,
            _ => null
        };

        public long? GetLong() => Value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            decimal dec => (long)dec,
            string s when long.TryParse(s, out var r) => r,
            _ => null
        };

        public float? GetFloat() => Value switch
        {
            double d => (float)d,
            int i => i,
            long l => l,
            decimal dec => (float)dec,
            string s when float.TryParse(s, out var r) => r,
            _ => null
        };

        public decimal? GetDecimal() => Value switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            double db => (decimal)db,
            string s when decimal.TryParse(s, out var r) => r,
            _ => null
        };

        public double? GetDouble() => Value switch
        {
            double d => d,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            string s when double.TryParse(s, out var r) => r,
            _ => null
        };

        // A string literal with NO numeric/bool coercion (unlike Parse) — "42" stays the string "42".
        // Built directly, without a JsonSerializer round-trip, so it stays reflection-free (Native AOT / trimming).
        public static NgElement FromStringLiteral(string text)
        {
            return new NgElement
            {
                Key = "",
                Value = text,
                ValueKind = text == null ? JsonValueKind.Null : JsonValueKind.String
            };
        }

        public static NgElement Parse(string literal)
        {
            object value;

            if (string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase))
                value = null;
            else if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
                value = true;
            else if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
                value = false;
            // Template literals always use '.' as the decimal separator, independent of the
            // thread culture — so parse invariantly.
            else if (int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                value = i;
            else if (decimal.TryParse(literal, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                value = d;
            else
                value = literal;

            return new NgElement
            {
                Key = "",
                Value = value,
                ValueKind = value switch
                {
                    null => JsonValueKind.Null,
                    bool => (bool)value ? JsonValueKind.True : JsonValueKind.False,
                    int or long or decimal or double => JsonValueKind.Number,
                    _ => JsonValueKind.String
                }
            };
        }

        public NgElement SelectToken(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            // Fast path: a plain property name (no '.', no '[') — the common case, {{ Name }} — resolves
            // with one dictionary lookup, skipping the Replace + Split allocation of the segment walk.
            if (path.IndexOf('.') < 0 && path.IndexOf('[') < 0)
            {
                return this.properties != null && this.properties.TryGetValue(path, out var direct)
                    ? direct
                    : null;
            }

            var segments = path
                .Replace("[", ".[").Split('.', StringSplitOptions.RemoveEmptyEntries);

            var current = this;

            foreach (var segment in segments)
            {
                if (segment.StartsWith("[") && segment.EndsWith("]"))
                {
                    if (!int.TryParse(segment.Trim('[', ']'), out int index))
                        return null;

                    if (index < 0 || index >= current.Children.Count)
                        return null;

                    current = current.Children[index];
                }
                else
                {
                    if (!current.Properties.TryGetValue(segment, out var next))
                        return null;

                    current = next;
                }
            }

            return current;
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is not NgElement other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (ValueKind != other.ValueKind)
                return false;

            return ValueKind switch
            {
                JsonValueKind.String => GetString() == other.GetString(),
                JsonValueKind.Number => GetDouble() == other.GetDouble(),
                JsonValueKind.True or JsonValueKind.False => GetBoolean() == other.GetBoolean(),
                JsonValueKind.Null => true,
                // Objects/arrays: reference equality only (the previous Value.Equals(Children)
                // compared a string to a list and was always false anyway).
                JsonValueKind.Object => false,
                JsonValueKind.Array => false,
                _ => Value?.Equals(other.Value) ?? other.Value == null
            };
        }

        public static NgElement FromJson(JsonElement jsonElement, NgElement parent = null, string key = "")
        {
            var ng = new NgElement
            {
                Key = key,
                Parent = parent,
                ValueKind = jsonElement.ValueKind,
                Value = JsonElementToObject(jsonElement)
            };

            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    ng.properties = new Dictionary<string, NgElement>();

                    foreach (var prop in jsonElement.EnumerateObject())
                    {
                        ng.properties[prop.Name] = FromJson(prop.Value, ng, prop.Name);
                    }

                    break;

                case JsonValueKind.Array:
                    ng.children = new List<NgElement>();
                    int i = 0;

                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        ng.children.Add(FromJson(item, ng, $"[{i++}]"));
                    }

                    break;

                default:
                    ng.Value = JsonElementToObject(jsonElement);
                    break;
            }

            return ng;
        }

        // Builds the NgElement tree directly from a CLR object graph via reflection, WITHOUT the
        // model -> JSON string -> JsonDocument round-trip FromJson needs. The FASTEST path (opt-in via
        // BuildFromTemplateAsync(string, NgElement)). Mirrors System.Text.Json's default mapping so it
        // renders identically for the common cases — integral -> long, float/double/decimal -> double,
        // DateTime/Guid/byte[] -> string, enum -> its number — and it honors [JsonPropertyName] and
        // [JsonIgnore]. NOT honored (use the object overload of BuildFromTemplateAsync for these):
        // custom [JsonConverter], [JsonNumberHandling], naming policies. Object/array nodes keep Value
        // null (FromJson stored the raw JSON there), so pipes that re-deserialize a whole object from
        // value.Value won't work with a FromObject-built tree.
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
                    ng.properties = new Dictionary<string, NgElement>();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        var name = entry.Key.ToString();
                        ng.properties[name] = FromObject(entry.Value, ng, name, depth + 1);
                    }
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
                    ng.ValueKind = JsonValueKind.Object;
                    ng.properties = new Dictionary<string, NgElement>();
                    foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!property.CanRead || property.GetIndexParameters().Length > 0)
                        {
                            continue;
                        }

                        var propValue = property.GetValue(value);
                        var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();

                        if (ignore != null &&
                            (ignore.Condition == JsonIgnoreCondition.Always
                             || (ignore.Condition == JsonIgnoreCondition.WhenWritingNull && propValue == null)))
                        {
                            continue;
                        }

                        var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                        ng.properties[name] = FromObject(propValue, ng, name, depth + 1);
                    }
                    return ng;
            }
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

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l
                    : element.TryGetDouble(out var d) ? d
                    : element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText() // Object, Array, Undefined…
            };
        }
    }
}
