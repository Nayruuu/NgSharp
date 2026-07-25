using System;

using Scriban;
using Scriban.Runtime;

namespace NgSharp.Benchmark.Realistic;

// Scriban port of the JSON "export" document (the TEXT-mode arena), byte-identical to the NgSharp
// reference (export.json.txt rendered in TemplateMode.Text). JSON literals delegate to the shared
// JsonFmt helpers — json (string literal), jnum (decimal on the FromObject decimal-as-double
// contract), iso (STJ DateTime scalar) — plus the shared Fmt.Date; bare bools/ints are already
// machine literals in Scriban. Same script.Import + PascalCase MemberRenamer as DevisScriban.
public static class ExportScriban
{
    private static readonly string Source = RealisticEngines.Load("Scriban/export.scriban");

    // Warm: parsed once, rendered per call (matches DevisScriban.Warm).
    private static readonly Template Parsed = Template.Parse(Source);

    // Cold: parse every call + render (matches DevisScriban.Cold).
    public static string Cold() => Render(Template.Parse(Source));

    public static string Warm() => Render(Parsed);

    private static string Render(Template template)
    {
        // Fresh ScriptObject + context per render, exactly as DevisScriban does — warm parity.
        var script = new ScriptObject();
        script.Import(RealisticEngines.DevisModel, renamer: member => member.Name);

        script.Import("json", new Func<string, string>(JsonFmt.Str));
        script.Import("jnum", new Func<decimal, string>(JsonFmt.Num));
        script.Import("iso", new Func<DateTime, string>(JsonFmt.Iso));
        script.Import("date", new Func<DateTime, string, string>((value, format) => Fmt.Date(value, format)));

        var context = new TemplateContext { MemberRenamer = member => member.Name };
        context.PushGlobal(script);

        return template.Render(context);
    }
}
