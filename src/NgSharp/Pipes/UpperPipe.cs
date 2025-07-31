using AngleSharp.Dom;

using System.Text.Json;

namespace NgSharp.Pipes
{
    public class UpperPipe : IPipe
    {
        public string PipeName => "upper";

        public string Transform(IElement childElement, NgElement value, string argument)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return string.Empty;

            return value.GetString().ToUpper();
        }
    }
}