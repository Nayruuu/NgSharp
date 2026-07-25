using System.Text.Json;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>pad</c> pipe: left-pads the value's string form with <c>'0'</c> to the width given by
/// the argument — <c>{{ Id | pad:6 }}</c> renders <c>42</c> as <c>000042</c>. Without a numeric
/// width the value passes through unchanged. A null value renders as empty.
/// </summary>
// The pipe grammar parses extra ':' segments, but a pipe only ever receives the FIRST argument
// (ExpressionEvaluator.EvaluatePipe) — the padding character therefore stays a fixed '0'.
public sealed class PadPipe : IPipe
{
    /// <inheritdoc/>
    public string PipeName => "pad";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        var text = value.GetString() ?? string.Empty;

        if (argument is null || int.TryParse(argument, out var width) == false || width <= 0)
        {
            return text;
        }

        return text.PadLeft(width, '0');
    }
}
