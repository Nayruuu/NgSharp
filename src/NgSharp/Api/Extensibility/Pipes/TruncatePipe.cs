using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>truncate</c> pipe: caps a string at N characters, ellipsis INCLUDED —
/// <c>{{ Summary | truncate:80 }}</c> is at most 80 characters, its last one <c>…</c> when cut.
/// Without an argument N is 50. A null value renders as empty.
/// </summary>
public sealed class TruncatePipe : IPipe
{
    private const int DEFAULT_LENGTH = 50;

    /// <inheritdoc/>
    public string PipeName => "truncate";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        var text = value.GetString() ?? string.Empty;

        var maxLength = DEFAULT_LENGTH;
        if (argument is not null && int.TryParse(argument, out var parsed))
        {
            maxLength = parsed;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 1)
        {
            return "…";
        }

        return text.Substring(0, maxLength - 1) + '…';
    }
}
