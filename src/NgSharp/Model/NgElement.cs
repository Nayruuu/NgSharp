using System;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NgSharp;

/// <summary>
/// The template data model — a lightweight, read-only JSON value tree. Build one from JSON with
/// <see cref="FromJson"/> or from a CLR object with <see cref="FromObject(object)"/>,
/// then navigate it with <see cref="SelectToken"/> and read leaves with the typed getters.
/// </summary>
// A node carries its own (kind, value): a scalar box, a live CLR object read lazily, or a LazyJsonNode.
public readonly partial struct NgElement : IEquatable<NgElement>
{
    #region Fields

    // 16-byte layout contract (the struct must keep returning in registers): one object field + the kind.
    private readonly object _carrier;   // scalar box | live CLR object (lazy) | LazyJsonNode

    private readonly JsonValueKind _kind;

    private static readonly IReadOnlyList<NgElement> EmptyChildren = Array.Empty<NgElement>();

    private static readonly IReadOnlyDictionary<string, NgElement> EmptyProperties =
        new ReadOnlyDictionary<string, NgElement>(new Dictionary<string, NgElement>());

    // Shared evaluator results — detached, immutable, so safe to share process-wide.
    internal static readonly NgElement True = new NgElement(JsonValueKind.True, true);

    internal static readonly NgElement False = new NgElement(JsonValueKind.False, false);

    internal static readonly NgElement Null = new NgElement(JsonValueKind.Null, null);

    #endregion

    #region Properties

    /// <summary>
    /// True only for the sentinel <c>default(NgElement)</c>, returned by <see cref="SelectToken"/> when
    /// the path doesn't exist. A value type can't be null, so this replaces the old <c>null</c> return —
    /// check it in a pipe/directive with <c>value.IsUndefined</c>.
    /// </summary>
    public bool IsUndefined => _kind == JsonValueKind.Undefined;

    /// <summary>
    /// Always empty: nodes are read lazily and don't record their position in the parent.
    /// </summary>
    public string Key => string.Empty;

    /// <summary>
    /// The scalar value of this node (string / bool / long / double / …), or null for object,
    /// array and null nodes.
    /// </summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    public object Value =>
        IsLazy ? null
        : IsDeferredStjScalar(_carrier) ? StjScalarString(_carrier)
        : _carrier;

    /// <summary>
    /// The JSON kind of this node (<see cref="JsonValueKind.Object"/>, <see cref="JsonValueKind.Array"/>,
    /// <see cref="JsonValueKind.String"/>, <see cref="JsonValueKind.Number"/>, …).
    /// </summary>
    public JsonValueKind ValueKind => _kind;

    /// <summary>
    /// The child nodes of an array (empty for any non-array node).
    /// </summary>
    public IReadOnlyList<NgElement> Children
    {
        get
        {
            if (_kind == JsonValueKind.Array && _carrier is not null)
            {
                var count = Count;
                if (count == 0)
                {
                    return EmptyChildren;
                }

                var items = new NgElement[count];
                for (var k = 0; k < count; k++)
                {
                    items[k] = ArrayItem(k);
                }

                return items;
            }

            return EmptyChildren;
        }
    }

    /// <summary>
    /// The properties of an object, keyed by name (empty for any non-object node).
    /// </summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    public IReadOnlyDictionary<string, NgElement> Properties
    {
        get
        {
            if (_kind == JsonValueKind.Object && _carrier is not null)
            {
                if (_carrier is LazyJsonNode json)
                {
                    var view = new Dictionary<string, NgElement>();
                    foreach (var property in json.Element.EnumerateObject())
                    {
                        view[property.Name] = MakeLazyJson(property.Value);
                    }

                    return view;
                }

                return LazyProperties(_carrier);
            }

            return EmptyProperties;
        }
    }

    /// <summary>
    /// The number of children (array items). Usable in templates as <c>{{ Items.Count }}</c> or in a
    /// condition <c>[if]="Items.Count &gt; 0"</c>. A real data property named <c>Count</c> takes precedence.
    /// </summary>
    public int Count =>
        _kind != JsonValueKind.Array || _carrier is null ? 0
        : _carrier is LazyJsonNode json ? json.Element.GetArrayLength()
        : LazyCount(_carrier);

    /// <summary>
    /// The string length for a string node, otherwise the child count. Usable in templates as
    /// <c>{{ Name.Length }}</c>. A real data property named <c>Length</c> takes precedence.
    /// </summary>
    public int Length => ValueKind == JsonValueKind.String ? (GetString()?.Length ?? 0) : Count;

    #endregion

    #region Constructors

    private NgElement(JsonValueKind kind, object value)
    {
        _carrier = value;
        _kind = kind;
    }

    #endregion

    #region Public methods

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
        bool boolValue => boolValue,
        string text when bool.TryParse(text, out var parsed) => parsed,
        _ => null
    };

    /// <summary>
    /// The value as a <see cref="DateTime"/>, parsing a string when needed.
    /// </summary>
    /// <returns>The date value, or null when it is neither a <see cref="DateTime"/> nor a parseable string.</returns>
    public DateTime? GetDateTime()
    {
        // Deferred date: unbox directly, but exclude Utc — DateTime.TryParse converts a trailing 'Z' to
        // local time, and the unboxed value must stay exactly what the string path produces.
        if (_carrier is DateTime direct && direct.Kind != DateTimeKind.Utc)
        {
            return direct;
        }

        return Value switch
        {
            DateTime dateTime => dateTime,
            string text when DateTime.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    // Numbers are only ever long/double (from JSON) or int/decimal (from Parse) — never float, so the getters have no float case.

    /// <summary>
    /// The value as an <c>int</c>, converting a numeric value or parsing a string.
    /// </summary>
    /// <returns>The integer value, or null when not convertible.</returns>
    public int? GetInt() => Value switch
    {
        int intValue => intValue,
        long longValue => (int)longValue,
        double doubleValue => (int)doubleValue,
        decimal decimalValue => (int)decimalValue,
        string text when int.TryParse(text, out var parsed) => parsed,
        _ => null
    };

    /// <summary>
    /// The value as a <c>long</c>, converting a numeric value or parsing a string.
    /// </summary>
    /// <returns>The long value, or null when not convertible.</returns>
    public long? GetLong() => Value switch
    {
        long longValue => longValue,
        int intValue => intValue,
        double doubleValue => (long)doubleValue,
        decimal decimalValue => (long)decimalValue,
        string text when long.TryParse(text, out var parsed) => parsed,
        _ => null
    };

    /// <summary>
    /// The value as a <c>float</c>, converting a numeric value or parsing a string.
    /// </summary>
    /// <returns>The float value, or null when not convertible.</returns>
    public float? GetFloat() => Value switch
    {
        double doubleValue => (float)doubleValue,
        int intValue => intValue,
        long longValue => longValue,
        decimal decimalValue => (float)decimalValue,
        string text when float.TryParse(text, out var parsed) => parsed,
        _ => null
    };

    /// <summary>
    /// The value as a <c>decimal</c>, converting a numeric value or parsing a string.
    /// </summary>
    /// <returns>The decimal value, or null when not convertible.</returns>
    public decimal? GetDecimal() => Value switch
    {
        decimal decimalValue => decimalValue,
        int intValue => intValue,
        long longValue => longValue,
        double doubleValue => (decimal)doubleValue,
        string text when decimal.TryParse(text, out var parsed) => parsed,
        _ => null
    };

    /// <summary>
    /// The value as a <c>double</c>, converting a numeric value or parsing a string.
    /// </summary>
    /// <returns>The double value, or null when not convertible.</returns>
    public double? GetDouble() => Value switch
    {
        double doubleValue => doubleValue,
        int intValue => intValue,
        long longValue => longValue,
        decimal decimalValue => (double)decimalValue,
        string text when double.TryParse(text, out var parsed) => parsed,
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
        => new NgElement(text is null ? JsonValueKind.Null : JsonValueKind.String, text);

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
        {
            value = null;
        }
        else if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
        }
        else if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
        }
        // Template literals always use '.' as the decimal separator, whatever the thread culture — parse invariantly.
        else if (int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            value = intValue;
        }
        else if (decimal.TryParse(literal, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            value = decimalValue;
        }
        else
        {
            value = literal;
        }

        var kind = value switch
        {
            null => JsonValueKind.Null,
            bool => (bool)value ? JsonValueKind.True : JsonValueKind.False,
            int or long or decimal or double => JsonValueKind.Number,
            _ => JsonValueKind.String
        };

        return new NgElement(kind, value);
    }

    #endregion

    #region Internal methods

    // The k-th item without materializing the Children list.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    internal NgElement ArrayItem(int k) =>
        _carrier is LazyJsonNode json ? JsonItem(json, k)
        : LazyItem(_carrier, k);

    // Computed numeric results are stored as double so they normalize via GetDouble() for comparison/equality.
    internal static NgElement FromNumber(double value) => new NgElement(JsonValueKind.Number, value);

    // Parsed numeric literals: integral stays long, fractional/exponent stays double.
    internal static NgElement FromParsedNumber(long value) => new NgElement(JsonValueKind.Number, BoxLong(value));

    internal static NgElement FromParsedNumber(double value) => new NgElement(JsonValueKind.Number, value);

    #endregion
}
