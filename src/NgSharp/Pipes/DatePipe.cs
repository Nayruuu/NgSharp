using AngleSharp.Dom;

using System;
using System.Text.Json;

namespace NgSharp.Pipes
{
    public class DatePipe : IPipe
    {
        public string PipeName => "date";

        public string Transform(IElement childElement, NgElement value, string argument)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return string.Empty;
            }

            DateTime? dateValue = value?.GetDateTime();

            if (dateValue.HasValue && !string.IsNullOrWhiteSpace(argument))
            {
                return dateValue.Value.ToString(argument);
            }

            return dateValue.HasValue ? dateValue.ToString() : string.Empty;
        }
    }
}