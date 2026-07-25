using System.Text.Json;

namespace NgSharp;

// Builds the NgElement value model from a System.Text.Json JsonElement (the reflection-free ingestion
// path). Members and items are read lazily on demand (see NgElement.LazyJson).
public readonly partial struct NgElement
{
    // Shared bool boxes. NOTE: WrapMember/AddObjectNode recover the bool KIND by reference identity to these.
    private static readonly object BoxedTrue = true;

    private static readonly object BoxedFalse = false;

    // Shared boxes for small longs, reused by both ingestion paths.
    private const long SMALL_LONG_MIN = -128;

    private const long SMALL_LONG_MAX = 1024;   // exclusive

    private static readonly object[] SmallLongBoxes = BuildSmallLongBoxes();

    /// <summary>
    /// Builds an <see cref="NgElement"/> from a <see cref="JsonElement"/> — the reflection-free
    /// ingestion path (Native AOT / trimming safe).
    /// </summary>
    /// <remarks>
    /// Only scalar leaves populate <see cref="NgElement.Value"/>; object and array nodes keep it null
    /// (matching <see cref="FromObject(object)"/>), so a pipe that re-deserializes a
    /// whole object from <c>value.Value</c> won't work here — read its fields with <see cref="NgElement.SelectToken"/>.
    /// The element is read lazily: keep its <see cref="JsonDocument"/> undisposed while the returned
    /// model is rendered (or pass a <see cref="JsonElement.Clone"/>).
    /// </remarks>
    /// <param name="jsonElement">The JSON to convert.</param>
    /// <returns>The root of the built model.</returns>
    public static NgElement FromJson(JsonElement jsonElement)
        => MakeLazyJson(jsonElement);

    private static object[] BuildSmallLongBoxes()
    {
        var boxes = new object[SMALL_LONG_MAX - SMALL_LONG_MIN];

        for (var i = 0; i < boxes.Length; i++)
        {
            boxes[i] = (long)(SMALL_LONG_MIN + i);
        }

        return boxes;
    }

    private static object BoxLong(long value)
        => value >= SMALL_LONG_MIN && value < SMALL_LONG_MAX ? SmallLongBoxes[value - SMALL_LONG_MIN] : value;

    private static object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? BoxLong(longValue)
                : element.TryGetDouble(out var doubleValue) ? doubleValue
                : element.GetRawText(),
            JsonValueKind.True => BoxedTrue,
            JsonValueKind.False => BoxedFalse,
            JsonValueKind.Null => null,
            _ => element.GetRawText() // Object, Array, Undefined…
        };
    }
}
