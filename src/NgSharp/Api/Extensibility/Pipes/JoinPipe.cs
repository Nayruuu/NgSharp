using System.Text;
using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>join</c> pipe: joins a collection's items into one string —
/// <c>{{ Tags | join:' · ' }}</c>; the separator defaults to <c>', '</c>. Items render as an
/// interpolation would (current-culture human formatting). A non-collection value renders as its
/// own string; null renders as empty.
/// </summary>
public sealed class JoinPipe : IPipe
{
    /// <inheritdoc/>
    public string PipeName => "join";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return value.ValueKind == JsonValueKind.Null ? string.Empty : value.GetString() ?? string.Empty;
        }

        var separator = argument ?? ", ";
        var children = value.Children;
        var builder = new StringBuilder(children.Count * 8);

        for (var i = 0; i < children.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(separator);
            }

            builder.Append(children[i].GetString());
        }

        return builder.ToString();
    }
}
