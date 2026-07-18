using System;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NgSharp
{
    /// <summary>
    /// The template data model — a lightweight, read-only JSON value tree. Build one from JSON with
    /// <see cref="FromJson"/> or from a CLR object with <see cref="FromObject(object, NgElement, string)"/>,
    /// then navigate it with <see cref="SelectToken"/> and read leaves with the typed getters.
    /// </summary>
    // This file holds the value model and its accessors; ingestion from JSON / CLR objects lives in the
    // NgElement.FromJson / .FromObject partials.
    public partial class NgElement
    {
        /// <summary>
        /// This node's key: its property name within the parent object, or <c>"[i]"</c> for an array
        /// item. Empty at the root.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The scalar value of this node (string / bool / long / double / …), or null for object,
        /// array and null nodes.
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// The parent node, or null at the root.
        /// </summary>
        public NgElement Parent { get; private set; }

        /// <summary>
        /// The JSON kind of this node (<see cref="JsonValueKind.Object"/>, <see cref="JsonValueKind.Array"/>,
        /// <see cref="JsonValueKind.String"/>, <see cref="JsonValueKind.Number"/>, …).
        /// </summary>
        public JsonValueKind ValueKind { get; private set; }

        // Array items / object properties. A scalar node allocates neither — it shares a static empty
        // instance — so the getters never return null. The tree is read-only from the outside; build it
        // via FromJson / FromObject.
        private List<NgElement> children;

        // Object properties as a flat array of child nodes (each carries its Key), not a per-node
        // Dictionary: for the handful of properties a typical object has, a linear Key scan is cheaper to
        // build (no hashing, one allocation instead of a dictionary's buckets+entries) and no slower to
        // resolve. The public Properties view is materialized lazily only if something reads it.
        private NgElement[] members;

        private IReadOnlyDictionary<string, NgElement> membersView;

        private static readonly IReadOnlyList<NgElement> EmptyChildren = Array.Empty<NgElement>();

        private static readonly IReadOnlyDictionary<string, NgElement> EmptyProperties =
            new ReadOnlyDictionary<string, NgElement>(new Dictionary<string, NgElement>());

        // Shared immutable boolean/null results. The evaluator returns these for comparison / logical /
        // null-path results instead of allocating a fresh NgElement each render. Safe to share: they are
        // leaf values (no Parent, no children) and are never mutated.
        internal static readonly NgElement True = new NgElement { Key = "", Value = true, ValueKind = JsonValueKind.True };

        internal static readonly NgElement False = new NgElement { Key = "", Value = false, ValueKind = JsonValueKind.False };

        internal static readonly NgElement Null = new NgElement { Key = "", Value = null, ValueKind = JsonValueKind.Null };

        /// <summary>
        /// The child nodes of an array (empty for any non-array node).
        /// </summary>
        public IReadOnlyList<NgElement> Children => this.children ?? EmptyChildren;

        /// <summary>
        /// The properties of an object, keyed by name (empty for any non-object node).
        /// </summary>
        public IReadOnlyDictionary<string, NgElement> Properties
        {
            get
            {
                if (this.members == null || this.members.Length == 0)
                {
                    return EmptyProperties;
                }

                if (this.membersView == null)
                {
                    // Last-writer-wins on duplicate keys, matching the former Dictionary build.
                    var view = new Dictionary<string, NgElement>(this.members.Length);
                    for (var i = 0; i < this.members.Length; i++)
                    {
                        view[this.members[i].Key] = this.members[i];
                    }
                    this.membersView = view;
                }

                return this.membersView;
            }
        }

        /// <summary>
        /// The number of children (array items or object properties). Usable in templates as
        /// <c>{{ Items.Count }}</c> or in a condition <c>[if]="Items.Count &gt; 0"</c>. A real data
        /// property named <c>Count</c> takes precedence.
        /// </summary>
        public int Count => Children.Count;

        /// <summary>
        /// The string length for a string node, otherwise the child count. Usable in templates as
        /// <c>{{ Name.Length }}</c>. A real data property named <c>Length</c> takes precedence.
        /// </summary>
        public int Length => ValueKind == JsonValueKind.String ? (GetString()?.Length ?? 0) : Children.Count;

        /// <summary>
        /// The full dotted path from the root to this node (e.g. <c>"Order.Items[0].Name"</c>).
        /// </summary>
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

        /// <summary>
        /// The value as a string (<c>Value.ToString()</c>).
        /// </summary>
        /// <returns>The string value, or null for a null node.</returns>
        public string GetString()
        {
            return Value?.ToString();
        }

        /// <summary>
        /// The value as a boolean, parsing a string when needed.
        /// </summary>
        /// <returns>The boolean value, or null when it is neither a bool nor a parseable string.</returns>
        public bool? GetBoolean() => Value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var r) => r,
            _ => null
        };

        /// <summary>
        /// The value as a <see cref="DateTime"/>, parsing a string when needed.
        /// </summary>
        /// <returns>The date value, or null when it is neither a <see cref="DateTime"/> nor a parseable string.</returns>
        public DateTime? GetDateTime() => Value switch
        {
            DateTime dt => dt,
            string s when DateTime.TryParse(s, out var r) => r,
            _ => null
        };

        // Value only ever holds long/double (from JSON) or int/decimal (from Parse) for numbers,
        // never float — so the numeric getters have no float case.

        /// <summary>
        /// The value as an <c>int</c>, converting a numeric value or parsing a string.
        /// </summary>
        /// <returns>The integer value, or null when not convertible.</returns>
        public int? GetInt() => Value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal dec => (int)dec,
            string s when int.TryParse(s, out var r) => r,
            _ => null
        };

        /// <summary>
        /// The value as a <c>long</c>, converting a numeric value or parsing a string.
        /// </summary>
        /// <returns>The long value, or null when not convertible.</returns>
        public long? GetLong() => Value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            decimal dec => (long)dec,
            string s when long.TryParse(s, out var r) => r,
            _ => null
        };

        /// <summary>
        /// The value as a <c>float</c>, converting a numeric value or parsing a string.
        /// </summary>
        /// <returns>The float value, or null when not convertible.</returns>
        public float? GetFloat() => Value switch
        {
            double d => (float)d,
            int i => i,
            long l => l,
            decimal dec => (float)dec,
            string s when float.TryParse(s, out var r) => r,
            _ => null
        };

        /// <summary>
        /// The value as a <c>decimal</c>, converting a numeric value or parsing a string.
        /// </summary>
        /// <returns>The decimal value, or null when not convertible.</returns>
        public decimal? GetDecimal() => Value switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            double db => (decimal)db,
            string s when decimal.TryParse(s, out var r) => r,
            _ => null
        };

        /// <summary>
        /// The value as a <c>double</c>, converting a numeric value or parsing a string.
        /// </summary>
        /// <returns>The double value, or null when not convertible.</returns>
        public double? GetDouble() => Value switch
        {
            double d => d,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            string s when double.TryParse(s, out var r) => r,
            _ => null
        };

        /// <summary>
        /// Wraps <paramref name="text"/> as a string node with no numeric/bool coercion — <c>"42"</c>
        /// stays the string <c>"42"</c>, unlike <see cref="Parse"/>. Built without a JsonSerializer
        /// round-trip, so it stays reflection-free (Native AOT / trimming safe).
        /// </summary>
        /// <param name="text">The literal text; null yields a null node.</param>
        /// <returns>A string (or null) node.</returns>
        public static NgElement FromStringLiteral(string text)
        {
            return new NgElement
            {
                Key = "",
                Value = text,
                ValueKind = text == null ? JsonValueKind.Null : JsonValueKind.String
            };
        }

        /// <summary>
        /// Parses a template literal into a node: <c>null</c> / <c>true</c> / <c>false</c> and
        /// culture-invariant numbers are coerced to their types; anything else stays a string.
        /// </summary>
        /// <param name="literal">The literal text to parse.</param>
        /// <returns>The parsed node.</returns>
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

        /// <summary>
        /// Resolves a path against this node: a plain property name, a dotted path, and array indices
        /// (<c>"Items[0].Name"</c>). Falls back to the computed members <see cref="Count"/> /
        /// <see cref="Length"/> when no real property of that name exists.
        /// </summary>
        /// <param name="path">The path to resolve.</param>
        /// <returns>The resolved node, or null when the path doesn't exist.</returns>
        public NgElement SelectToken(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            // Fast path: a plain property name (no '.', no '[') — the common case, {{ Name }} — resolves
            // with one dictionary lookup, skipping the Replace + Split allocation of the segment walk.
            if (path.IndexOf('.') < 0 && path.IndexOf('[') < 0)
            {
                return ResolveMember(this, path);
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
                    current = ResolveMember(current, segment);
                    if (current == null)
                        return null;
                }
            }

            return current;
        }

        // Resolves a named member: a real data property first, then the built-in computed members
        // (Count / Length) as a fallback so they never shadow actual data.
        private static NgElement ResolveMember(NgElement element, string name)
        {
            var members = element.members;
            if (members != null)
            {
                // Scan from the end so a duplicate key resolves to the last one written, as the former
                // Dictionary did (dict[key] = value overwrote).
                for (var i = members.Length - 1; i >= 0; i--)
                {
                    if (members[i].Key == name)
                        return members[i];
                }
            }

            if (name == "Count")
                return Number(element.Count);

            if (name == "Length")
                return Number(element.Length);

            return null;
        }

        private static NgElement Number(int value)
            => new NgElement { Key = "", Value = value, ValueKind = JsonValueKind.Number };

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            // Must agree with Equals, which compares numbers by GetDouble() and strings/bools by their
            // typed value — so hash the same normalized value, not the raw boxed Value (an int 2 and a
            // double 2.0 are Equal and must hash alike). Object/array/null hash to 0 (Equals treats
            // objects/arrays by reference, which the bucket resolves).
            return ValueKind switch
            {
                JsonValueKind.Number => (GetDouble() ?? 0d).GetHashCode(),
                JsonValueKind.String => GetString()?.GetHashCode() ?? 0,
                JsonValueKind.True or JsonValueKind.False => (GetBoolean() ?? false).GetHashCode(),
                _ => 0,
            };
        }

        /// <summary>
        /// Value equality: scalars (string / number / bool / null) compare by value; objects and arrays
        /// compare by reference.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns><c>true</c> when the two nodes are equal.</returns>
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
    }
}
