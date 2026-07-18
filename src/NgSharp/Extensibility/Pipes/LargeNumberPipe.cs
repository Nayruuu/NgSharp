using System;
using System.Collections.Generic;

using System.Text.Json;

namespace NgSharp.Pipes
{
    /// <summary>
    /// Built-in <c>largeNumber</c> pipe: abbreviates a large number with a magnitude suffix
    /// (<c>K</c>, <c>M</c>, <c>B</c>, <c>T</c>, <c>Q</c>) — e.g. <c>1500</c> becomes <c>1.5K</c>. A null
    /// value renders as <c>0</c>.
    /// </summary>
    public class LargeNumberPipe : IPipe
    {
        /// <inheritdoc/>
        public string PipeName => "largeNumber";

        private readonly Dictionary<string, double> powers = new()
        {
            { "Q", Math.Pow(10, 15) },
            { "T", Math.Pow(10, 12) },
            { "B", Math.Pow(10, 9) },
            { "M", Math.Pow(10, 6) },
            { "K", Math.Pow(10, 3) }
        };

        /// <inheritdoc/>
        public string Transform(string tagName, NgElement value, string argument)
        {
            if (value is null || value.ValueKind == JsonValueKind.Null)
            {
                return "0";
            }
            else
            {
                decimal? numberValue = value?.GetDecimal();

                if (numberValue.HasValue)
                {
                    var rounder = Math.Pow(10, 1);
                    var isNegative = numberValue < 0;
                    var absoluteValue = (double)Math.Abs(numberValue.Value);

                    foreach (var power in powers)
                    {
                        var reduced = (double)absoluteValue / power.Value;
                        reduced = Math.Round(reduced * rounder) / rounder;

                        if (reduced >= 1)
                        {
                            return $"{(isNegative ? "-" : "")}{reduced}{power.Key}";
                        }
                    }
                }

                return numberValue.HasValue ? numberValue.ToString() : "0";
            }
        }
    }
}

