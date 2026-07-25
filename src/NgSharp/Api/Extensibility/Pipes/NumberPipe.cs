using System;
using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>number</c> pipe: formats a numeric value with a .NET numeric format string —
/// <c>{{ Price | number:'C2' }}</c>. A null value is formatted as <c>0</c>.
/// </summary>
public sealed class NumberPipe : IPipe, ISpanPipe
{
    /// <inheritdoc/>
    public string PipeName => "number";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return 0.ToString(argument);
        }

        var numberValue = value.GetDecimal();

        return numberValue.Value.ToString(argument);
    }

    // TryFormat with a null provider matches ToString(argument)'s current-culture formatting exactly.
    bool ISpanPipe.TryTransform(string tagName, NgElement value, string argument, Span<char> destination, out int written)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return 0.TryFormat(destination, out written, argument);
        }

        var numberValue = value.GetDecimal();

        return numberValue.Value.TryFormat(destination, out written, argument);
    }
}
