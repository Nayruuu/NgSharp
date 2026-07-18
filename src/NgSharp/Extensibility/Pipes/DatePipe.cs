
using System;
using System.Text.Json;

namespace NgSharp.Pipes
{
    /// <summary>
    /// Built-in <c>date</c> pipe: formats a date value with an optional .NET format string —
    /// <c>{{ CreatedAt | date:'yyyy-MM-dd' }}</c>. A null value renders as empty.
    /// </summary>
    public class DatePipe : IPipe
    {
        /// <inheritdoc/>
        public string PipeName => "date";

        /// <inheritdoc/>
        public string Transform(string tagName, NgElement value, string argument)
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