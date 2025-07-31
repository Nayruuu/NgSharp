using AngleSharp.Dom;

using System.Text.Json;

namespace NgSharp.Pipes
{
    public class NumberPipe : IPipe
    {
        public string PipeName => "number";

        public string Transform(IElement childElement, NgElement value, string argument)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return 0.ToString(argument);

            decimal? numberValue = value?.GetDecimal();

            return numberValue.Value.ToString(argument);
        }
    }
}