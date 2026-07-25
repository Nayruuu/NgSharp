using System;
using System.Threading.Tasks;

using Fluid;
using Fluid.Values;

namespace NgSharp.Benchmark.Realistic;

// Fluid / Liquid port of the "listes-cartes" (card list) document. Produces byte-identical output to the
// NgSharp reference (RealisticEngines.NgSharp_Cartes_*) by delegating ALL number/date/upper/largeNumber
// formatting to the shared Fmt helpers through custom Liquid filters — so the engines differ only in
// parse/render, never in formatting. Same FluidParser + UnsafeMemberAccessStrategy setup as DevisFluid.
public static class CartesFluid
{
    private static readonly string Template = RealisticEngines.Load("Fluid/cartes.fluid.liquid");
    private static readonly FluidParser Parser = new FluidParser();
    private static readonly Fluid.TemplateOptions Options = BuildOptions();
    private static readonly IFluidTemplate Compiled = Parse();

    // Cold = parse the template string every call, then render (matches Engines.Fluid_Cold).
    public static string Cold()
    {
        Parser.TryParse(Template, out var template, out _);

        return template.Render(new TemplateContext(RealisticEngines.CartesModel, Options));
    }

    // Warm = pre-parsed once (static skeleton), render only each call (matches Engines.Fluid_Warm).
    public static string Warm()
        => Compiled.Render(new TemplateContext(RealisticEngines.CartesModel, Options));

    private static Fluid.TemplateOptions BuildOptions()
    {
        var options = new Fluid.TemplateOptions();
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();

        options.Filters.AddFilter("number", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(
                Fmt.Number(input.ToNumberValue(), arguments.At(0).ToStringValue()))));

        // Overrides Fluid's built-in date filter so the .NET format string is honoured verbatim (not Ruby strftime).
        options.Filters.AddFilter("date", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(
                Fmt.Date(ToDateTime(input), arguments.At(0).ToStringValue()))));

        options.Filters.AddFilter("upper", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Fmt.Upper(input.ToStringValue()))));

        options.Filters.AddFilter("largeNumber", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Fmt.LargeNumber(input.ToNumberValue()))));

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
