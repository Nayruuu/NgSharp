using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NgSharp.Benchmark.Realistic;

// Shared formatting used by the Fluid / Handlebars / Scriban custom filters so ALL FOUR engines produce
// byte-identical output — a faithful replica of NgSharp's built-in number / date / upper / largeNumber
// pipes. The engines then differ only in template parse + render (what the benchmark measures), never in
// number/date formatting. Culture is pinned to fr-FR (the real documents are French: "1 234,56", dates
// dd/MM/yyyy), matching what NgSharp's pipes pick up from CurrentCulture.
public static class Fmt
{
    internal static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    // NgSharp LargeNumberPipe: abbreviate with a magnitude suffix (K/M/B/T/Q), one decimal, else the number.
    private static readonly (string Suffix, double Power)[] Powers =
    {
        ("Q", 1e15), ("T", 1e12), ("B", 1e9), ("M", 1e6), ("K", 1e3),
    };

    // NgSharp NumberPipe: null -> 0.ToString(fmt); else value.ToString(fmt) (CurrentCulture).
    public static string Number(decimal? value, string format)
        => value.HasValue ? value.Value.ToString(format) : 0.ToString(format);

    // NgSharp DatePipe: null -> ""; with a format -> value.ToString(fmt); else value.ToString().
    public static string Date(DateTime? value, string format)
    {
        if (value.HasValue == false)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(format) ? value.Value.ToString() : value.Value.ToString(format);
    }

    // NgSharp UpperPipe: null -> ""; else value.ToUpper() (CurrentCulture).
    public static string Upper(string value) => value?.ToUpper() ?? string.Empty;

    public static string LargeNumber(decimal? value)
    {
        if (value.HasValue == false)
        {
            return "0";
        }

        var isNegative = value < 0;
        var absolute = (double)Math.Abs(value.Value);

        foreach (var (suffix, power) in Powers)
        {
            var reduced = absolute / power;
            reduced = Math.Round(reduced * 10) / 10;

            if (reduced >= 1)
            {
                return $"{(isNegative ? "-" : string.Empty)}{reduced}{suffix}";
            }
        }

        return value.Value.ToString();
    }

    [ModuleInitializer]
    internal static void PinCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = Fr;
        CultureInfo.CurrentCulture = Fr;
    }
}
