using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>default</c> pipe: substitutes the argument when the value is undefined, null, or a
/// blank string — <c>{{ Nickname | default:'—' }}</c>. <c>false</c> and <c>0</c> are values, never
/// substituted.
/// </summary>
public sealed class DefaultPipe : IPipe
{
    /// <inheritdoc/>
    public string PipeName => "default";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.IsUndefined || value.ValueKind == JsonValueKind.Null)
        {
            return argument ?? string.Empty;
        }

        var text = value.GetString();

        if (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(text))
        {
            return argument ?? string.Empty;
        }

        return text ?? string.Empty;
    }
}
