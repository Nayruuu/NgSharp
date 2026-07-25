using System.Text.Json;
using System.Globalization;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>titlecase</c> pipe: capitalizes each word of a string and lowercases the rest —
/// <c>{{ Title | titlecase }}</c> renders <c>HELLO world</c> as <c>Hello World</c> (Angular
/// behavior, current culture). A null value renders as empty.
/// </summary>
public sealed class TitleCasePipe : IPipe
{
    /// <inheritdoc/>
    public string PipeName => "titlecase";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        var text = value.GetString();
        if (text is null)
        {
            return string.Empty;
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;

        return textInfo.ToTitleCase(textInfo.ToLower(text));
    }
}
