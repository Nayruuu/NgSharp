using System;
using System.Threading.Tasks;

using Fluid;
using Fluid.Values;

namespace NgSharp.Benchmark.Realistic;

// Fluid / Liquid port of the JSON "export" document (the TEXT-mode arena). Produces byte-identical
// output to the NgSharp reference (RealisticEngines.NgSharp_Export_*) by delegating every JSON literal
// to the shared JsonFmt helpers through custom filters — json (string literal), jnum (decimal on the
// FromObject decimal-as-double contract), iso (STJ DateTime scalar) — plus the shared Fmt.Date, so the
// engines differ only in parse/render, never in formatting. Mirrors DevisFluid (same FluidParser +
// UnsafeMemberAccessStrategy, Cold = re-parse per call, Warm = pre-parsed).
public static class ExportFluid
{
    private static readonly string Template = RealisticEngines.Load("Fluid/export.fluid.liquid");
    private static readonly FluidParser Parser = new FluidParser();
    private static readonly Fluid.TemplateOptions Options = BuildOptions();
    private static readonly IFluidTemplate Compiled = Parse();

    // Cold = parse the template string every call, then render (matches DevisFluid.Cold).
    public static string Cold()
    {
        Parser.TryParse(Template, out var template, out _);

        return template.Render(new TemplateContext(RealisticEngines.DevisModel, Options));
    }

    // Warm = pre-parsed once (static skeleton), render only each call (matches DevisFluid.Warm).
    public static string Warm()
        => Compiled.Render(new TemplateContext(RealisticEngines.DevisModel, Options));

    private static Fluid.TemplateOptions BuildOptions()
    {
        var options = new Fluid.TemplateOptions();
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();

        options.Filters.AddFilter("json", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(JsonFmt.Str(input.ToStringValue()))));

        options.Filters.AddFilter("jnum", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(JsonFmt.Num(input.ToNumberValue()))));

        options.Filters.AddFilter("iso", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(
                ToDateTime(input) is { } value ? JsonFmt.Iso(value) : string.Empty)));

        // Overrides Fluid's built-in date filter so the .NET format string is honoured verbatim (not Ruby strftime).
        options.Filters.AddFilter("date", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(
                Fmt.Date(ToDateTime(input), arguments.At(0).ToStringValue()))));

        return options;
    }

    private static DateTime? ToDateTime(FluidValue input)
    {
        var value = input.ToObjectValue();

        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            _ => null,
        };
    }

    private static IFluidTemplate Parse()
    {
        Parser.TryParse(Template, out var template, out _);

        return template;
    }
}
