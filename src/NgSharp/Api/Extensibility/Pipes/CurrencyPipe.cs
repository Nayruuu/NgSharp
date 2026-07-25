using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;

namespace NgSharp.Pipes;

/// <summary>
/// Built-in <c>currency</c> pipe: formats a number as a currency amount in the CURRENT culture's
/// format, with the currency pinned by an ISO 4217 argument — <c>{{ Price | currency:'EUR' }}</c>
/// renders <c>12,50 €</c> under fr-FR and <c>€12.50</c> under en-US. Without an argument it is the
/// plain current-culture <c>C</c> format. A null value is formatted as <c>0</c>; a non-numeric value
/// follows <see cref="NumberPipe"/>'s contract.
/// </summary>
public sealed class CurrencyPipe : IPipe
{
    private static readonly Dictionary<string, (string Symbol, int Decimals)> Currencies = new Dictionary<string, (string, int)>
    {
        { "EUR", ("€", 2) },
        { "USD", ("$", 2) },
        { "GBP", ("£", 2) },
        { "JPY", ("¥", 0) },
        { "CHF", ("CHF", 2) },
        { "CAD", ("$", 2) },
        { "AUD", ("$", 2) }
    };

    /// <inheritdoc/>
    public string PipeName => "currency";

    /// <inheritdoc/>
    public string Transform(string tagName, NgElement value, string argument)
    {
        var format = ResolveFormat(argument);

        if (value.ValueKind == JsonValueKind.Null)
        {
            return 0.ToString("C", format);
        }

        var numberValue = value.GetDecimal();

        return numberValue.Value.ToString("C", format);
    }

    // The current culture keeps every formatting decision (separators, symbol placement); the
    // currency pins its symbol AND its decimal count (a JPY amount has no cents in any locale) — an
    // unknown ISO code becomes its own symbol and keeps the culture's decimals.
    private static NumberFormatInfo ResolveFormat(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return CultureInfo.CurrentCulture.NumberFormat;
        }

        var isoCode = argument.Trim().ToUpperInvariant();
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();

        if (Currencies.TryGetValue(isoCode, out var currency))
        {
            format.CurrencySymbol = currency.Symbol;
            format.CurrencyDecimalDigits = currency.Decimals;
        }
        else
        {
            format.CurrencySymbol = isoCode;
        }

        return format;
    }
}
