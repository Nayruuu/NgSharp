
using System.Text.Json;

namespace NgSharp.Pipes
{
    /// <summary>
    /// Built-in <c>upper</c> pipe: uppercases a string value — <c>{{ Name | upper }}</c>. A null value
    /// renders as empty.
    /// </summary>
    public class UpperPipe : IPipe
    {
        /// <inheritdoc/>
        public string PipeName => "upper";

        /// <inheritdoc/>
        public string Transform(string tagName, NgElement value, string argument)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return string.Empty;

            return value.GetString().ToUpper();
        }
    }
}