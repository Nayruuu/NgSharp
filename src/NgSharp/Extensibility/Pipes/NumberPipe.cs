
using System.Text.Json;

namespace NgSharp.Pipes
{
    /// <summary>
    /// Built-in <c>number</c> pipe: formats a numeric value with a .NET numeric format string —
    /// <c>{{ Price | number:'C2' }}</c>. A null value is formatted as <c>0</c>.
    /// </summary>
    public class NumberPipe : IPipe
    {
        /// <inheritdoc/>
        public string PipeName => "number";

        /// <inheritdoc/>
        public string Transform(string tagName, NgElement value, string argument)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return 0.ToString(argument);

            decimal? numberValue = value?.GetDecimal();

            return numberValue.Value.ToString(argument);
        }
    }
}