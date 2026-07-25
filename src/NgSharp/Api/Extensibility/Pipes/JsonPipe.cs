using System;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>json</c> pipe: writes the value as its complete JSON literal — a string comes out
/// quoted and JSON-escaped (quotes, backslashes, control characters), numbers are culture-invariant,
/// booleans lowercase, null is <c>null</c>; objects and arrays serialize recursively. Made for
/// <see cref="TemplateMode.Text"/> JSON templates: <c>"name":{{ Name | json }}</c> — the quotes
/// come from the pipe.
/// </summary>
// Hand-written literal writer (a ValueKind switch, no JsonSerializer): reflection-free, so it stays
// Native AOT / trimming safe like the rest of the engine.
public sealed class JsonPipe : IPipe
{
    /// <inheritdoc/>
    public string PipeName => "json";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        var builder = new StringBuilder(32);

        WriteLiteral(builder, value, 0);

        return builder.ToString();
    }

    private static void WriteLiteral(StringBuilder builder, NgElement value, int depth)
    {
        // Same ceiling as System.Text.Json's default MaxDepth — a cyclic CLR model fails loud
        // instead of overflowing the stack.
        if (depth > 64)
        {
            throw new InvalidOperationException("The json pipe exceeded the maximum depth of 64 — is the model cyclic?");
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                builder.Append("null");
                break;

            case JsonValueKind.True:
                builder.Append("true");
                break;

            case JsonValueKind.False:
                builder.Append("false");
                break;

            case JsonValueKind.Number:
                WriteNumber(builder, value);
                break;

            case JsonValueKind.Array:
                builder.Append('[');
                var children = value.Children;
                for (var i = 0; i < children.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    WriteLiteral(builder, children[i], depth + 1);
                }

                builder.Append(']');

                break;

            case JsonValueKind.Object:
                builder.Append('{');
                var first = true;
                foreach (var property in value.Properties)
                {
                    if (first == false)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(builder, property.Key);
                    builder.Append(':');
                    WriteLiteral(builder, property.Value, depth + 1);
                }

                builder.Append('}');

                break;

            default:   // String — and any exotic scalar falls back to its string form.
                var text = value.GetString();
                if (text is null)
                {
                    builder.Append("null");
                }
                else
                {
                    WriteString(builder, text);
                }

                break;
        }
    }

    // NgElement numbers are int/long/double/decimal; the fallback covers a JSON number kept as raw
    // text (out of double range) — already a valid invariant literal.
    private static void WriteNumber(StringBuilder builder, NgElement value)
    {
        switch (value.Value)
        {
            case int intValue:
                builder.Append(intValue.ToString(CultureInfo.InvariantCulture));
                break;
            case long longValue:
                builder.Append(longValue.ToString(CultureInfo.InvariantCulture));
                break;
            case double doubleValue:
                builder.Append(doubleValue.ToString(CultureInfo.InvariantCulture));
                break;
            case decimal decimalValue:
                builder.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                builder.Append(value.GetString());
                break;
        }
    }

    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');

        foreach (var ch in text)
        {
            switch (ch)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (ch < ' ')
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
