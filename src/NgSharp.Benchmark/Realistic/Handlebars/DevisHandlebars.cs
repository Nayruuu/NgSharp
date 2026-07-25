using System;

using HandlebarsDotNet;

namespace NgSharp.Benchmark.Realistic;

// Handlebars.NET port of the devis template. Renders byte-identical output to the NgSharp reference
// (RealisticEngines.NgSharp_Devis_*) so the two engines can be benchmarked on the same document.
//
// Byte-identity relies on three things:
//  * All number/date/upper/largeNumber formatting is delegated to the shared Fmt helpers (fr-FR pinned),
//    never reimplemented — so the engines differ only in template parse + render, never in formatting.
//  * HTML-escaping is disabled (NoEscape) because NgSharp does not HTML-encode interpolation output;
//    e.g. the apostrophe in a term ("d'émission") must stay raw, not become "d&#x27;émission".
//  * The 'truthy' helper replicates NgSharp's [if] coercion (NgElement.GetBoolean() ?? false): only a
//    bool (or a parseable "true"/"false" string) is truthy, everything else is false. This is why a
//    decimal Discount never renders its span — exactly as NgSharp evaluates [if]="Discount". The five
//    plain bool conditionals (HasDiscount/InStock/OnOption/Highlighted/HasOptions) use native {{#if}},
//    which already matches NgSharp's bool semantics.
public static class DevisHandlebars
{
    private static readonly string Template = RealisticEngines.Load("Handlebars/devis.hbs");

    private static readonly IHandlebars Hb = Build();

    private static readonly HandlebarsTemplate<object, object> Compiled = Hb.Compile(Template);

    // Cold: compile the template string every call + invoke (matches Engines.Handlebars_Cold — helpers
    // are registered once on the shared instance; the codegen cost measured is the per-call compile).
    public static string Cold() => (string)Hb.Compile(Template)(RealisticEngines.DevisModel);

    // Warm: compiled once, invoke only (matches Engines.Handlebars_Warm).
    public static string Warm() => (string)Compiled(RealisticEngines.DevisModel);

    private static IHandlebars Build()
    {
        var hb = Handlebars.Create(new HandlebarsConfiguration { NoEscape = true });

        // Named 'num', not 'number': Handlebars.Net ships a built-in 'number' helper that shadows a custom
        // registration (it swallows the positional args). Maps NgSharp's number pipe.
        hb.RegisterHelper("num", (context, args) => Fmt.Number(Convert.ToDecimal(args[0]), args[1].ToString()));
        hb.RegisterHelper("date", (context, args) => Fmt.Date(Convert.ToDateTime(args[0]), args[1].ToString()));
        hb.RegisterHelper("upper", (context, args) => Fmt.Upper(args[0]?.ToString()));
        hb.RegisterHelper("largeNumber", (context, args) => Fmt.LargeNumber(Convert.ToDecimal(args[0])));
        hb.RegisterHelper("truthy", (context, args) => args[0] switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var r) => r,
            _ => false,
        });

        return hb;
    }
}
