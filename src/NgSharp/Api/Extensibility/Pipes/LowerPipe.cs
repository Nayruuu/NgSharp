using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>lower</c> pipe: lowercases a string value — <c>{{ Name | lower }}</c>. A null value
/// renders as empty.
/// </summary>
public sealed class LowerPipe : IPipe
{
    /// <inheritdoc/>
    public string PipeName => "lower";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return value.GetString().ToLower();
    }
}
