using System;

using HandlebarsDotNet;

namespace NgSharp.Benchmark.Realistic;

// Handlebars.NET port of the JSON "export" document (the TEXT-mode arena). Renders byte-identical
// output to the NgSharp reference (RealisticEngines.NgSharp_Export_*):
//  * Every JSON literal delegates to the shared JsonFmt helpers — json (string literal), jnum
//    (decimal on the FromObject decimal-as-double contract), jbool (lowercase machine bool, because a
//    bare {{Bool}} would render C#'s "True"), iso (STJ DateTime scalar) — plus the shared Fmt.Date.
//  * HTML-escaping is disabled (NoEscape): the json helper's quotes and the raw French text must
//    stay untouched.
//  * 'gt0' replicates NgSharp's @if (Discount > 0) comparison for the conditional discount property
//    (the subexpression pattern of DevisHandlebars' 'truthy').
public static class ExportHandlebars
{
    private static readonly string Template = RealisticEngines.Load("Handlebars/export.hbs");

    private static readonly IHandlebars Hb = Build();

    private static readonly HandlebarsTemplate<object, object> Compiled = Hb.Compile(Template);

    // Cold: compile the template string every call + invoke (matches DevisHandlebars.Cold).
    public static string Cold() => (string)Hb.Compile(Template)(RealisticEngines.DevisModel);

    // Warm: compiled once, invoke only (matches DevisHandlebars.Warm).
    public static string Warm() => (string)Compiled(RealisticEngines.DevisModel);

    private static IHandlebars Build()
    {
        var hb = Handlebars.Create(new HandlebarsConfiguration { NoEscape = true });

        hb.RegisterHelper("json", (context, args) => JsonFmt.Str(args[0]?.ToString()));
        hb.RegisterHelper("jnum", (context, args) => JsonFmt.Num(Convert.ToDecimal(args[0])));
        hb.RegisterHelper("jbool", (context, args) => JsonFmt.Bool((bool)args[0]));
        hb.RegisterHelper("iso", (context, args) => JsonFmt.Iso(Convert.ToDateTime(args[0])));
        hb.RegisterHelper("date", (context, args) => Fmt.Date(Convert.ToDateTime(args[0]), args[1].ToString()));
        hb.RegisterHelper("gt0", (context, args) => Convert.ToDecimal(args[0]) > 0);

        // Handlebars.Net cannot lex a literal '}' glued to a mustache close ("}}}" -> "Starting and
        // ending handlebars do not match"), which a minified JSON template produces at every object
        // close after an interpolation — even a comment between them fails. The helper EMITS the
        // closing brace instead: {{jnum TotalHT}}{{cb}}, renders "…324},".
        hb.RegisterHelper("cb", (context, args) => "}");

        return hb;
    }
}
