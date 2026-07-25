using System;

using HandlebarsDotNet;

namespace NgSharp.Benchmark.Realistic;

// Handlebars.NET port of the fiche-produit template. Renders byte-identical output to the NgSharp reference
// (RealisticEngines.NgSharp_Fiche_*) so the two engines can be benchmarked on the same document.
//
// Same setup as DevisHandlebars: number/date/upper/largeNumber all delegate to the shared Fmt helpers
// (fr-FR pinned) and HTML-escaping is disabled (NoEscape) — so the engines differ only in template parse +
// render, never in formatting. Every conditional here is a plain bool field (Available / IsDigital /
// HasLighting / HasAudienceData / IsPremium / NearTransport / Highlighted / LastMinute), so native {{#if}}
// already matches NgSharp's [if] bool semantics — no 'truthy' coercion helper needed.
public static class FicheHandlebars
{
    private static readonly string Template = RealisticEngines.Load("Handlebars/fiche.hbs");

    private static readonly IHandlebars Hb = Build();

    private static readonly HandlebarsTemplate<object, object> Compiled = Hb.Compile(Template);

    // Cold: compile the template string every call + invoke (matches Engines.Handlebars_Cold — helpers are
    // registered once on the shared instance; the codegen cost measured is the per-call compile).
    public static string Cold() => (string)Hb.Compile(Template)(RealisticEngines.FicheModel);

    // Warm: compiled once, invoke only.
    public static string Warm() => (string)Compiled(RealisticEngines.FicheModel);

    private static IHandlebars Build()
    {
        var hb = Handlebars.Create(new HandlebarsConfiguration { NoEscape = true });

        // Named 'num', not 'number': Handlebars.Net ships a built-in 'number' helper that shadows a custom
        // registration (it swallows the positional args). Maps NgSharp's number pipe.
        hb.RegisterHelper("num", (context, args) => Fmt.Number(Convert.ToDecimal(args[0]), args[1].ToString()));
        hb.RegisterHelper("date", (context, args) => Fmt.Date(Convert.ToDateTime(args[0]), args[1].ToString()));
        hb.RegisterHelper("upper", (context, args) => Fmt.Upper(args[0]?.ToString()));
        hb.RegisterHelper("largeNumber", (context, args) => Fmt.LargeNumber(Convert.ToDecimal(args[0])));

        return hb;
    }
}
