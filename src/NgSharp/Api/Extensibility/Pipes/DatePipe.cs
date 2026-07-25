using System;
using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>date</c> pipe: formats a date value with an optional .NET format string —
/// <c>{{ CreatedAt | date:'yyyy-MM-dd' }}</c>. A null value renders as empty.
/// </summary>
public sealed class DatePipe : IPipe, ISpanPipe
{
    /// <inheritdoc/>
    public string PipeName => "date";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        var dateValue = value.GetDateTime();

        if (dateValue.HasValue && string.IsNullOrWhiteSpace(argument) == false)
        {
            return dateValue.Value.ToString(argument);
        }

        return dateValue.HasValue ? dateValue.ToString() : string.Empty;
    }

    // Explicit-format form only (TryFormat + null provider ≡ ToString(argument)); the no-argument form stays on the string path.
    bool ISpanPipe.TryTransform(string tagName, NgElement value, string argument, Span<char> destination, out int written)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            written = 0;

            return true;
        }

        var dateValue = value.GetDateTime();

        if (dateValue.HasValue && string.IsNullOrWhiteSpace(argument) == false)
        {
            return dateValue.Value.TryFormat(destination, out written, argument);
        }

        written = 0;

        return false;
    }
}
