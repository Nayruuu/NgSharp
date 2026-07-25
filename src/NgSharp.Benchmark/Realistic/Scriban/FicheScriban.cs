using System;

using Scriban;
using Scriban.Runtime;

namespace NgSharp.Benchmark.Realistic;

// Scriban port of the realistic "fiche-produit" (product sheet) document, producing byte-identical output
// to the NgSharp reference (fiche.ngsharp.html). Structure is mirrored with {{ for }}/{{ if }}; the
// number/date/upper/largeNumber pipes delegate to the shared Fmt helpers (fr-FR, pinned) so formatting is
// identical across every engine and the benchmark measures only parse + render. Members are accessed
// PascalCase via the same `member => member.Name` renamer used by DevisScriban so both model members
// (Name, BasePrice) and the loop-scoped fields (spec.Label, slot.Price) resolve as-is.
public static class FicheScriban
{
    private static readonly string Source = RealisticEngines.Load("Scriban/fiche.scriban");

    // Warm: parsed once, rendered per call (matches Engines.Scriban_Warm).
    private static readonly Template Parsed = Template.Parse(Source);

    // Cold: parse every call + render (matches Engines.Scriban_Cold).
    public static string Cold() => Render(Template.Parse(Source));

    public static string Warm() => Render(Parsed);

    private static string Render(Template template)
    {
        // Fresh ScriptObject + context per render, exactly as Scriban's Render(model, renamer) overload
        // does internally — so the warm cost mirrors the catalog Scriban_Warm path.
        var script = new ScriptObject();
        script.Import(RealisticEngines.FicheModel, renamer: member => member.Name);

        // Custom pipe functions. In a Scriban pipe `{{ x | number 'N2' }}` the piped value is the first
        // argument and the literal the second — matching Fmt.Number(decimal?, string) etc. These names are
        // imported verbatim (no renamer on the key), so the template calls them exactly as written; the
        // largeNumber pipe is snake-cased to `large_number`, Scriban's default identifier form.
        script.Import("number", new Func<decimal, string, string>((value, format) => Fmt.Number(value, format)));
        script.Import("date", new Func<DateTime, string, string>((value, format) => Fmt.Date(value, format)));
        script.Import("upper", new Func<string, string>(Fmt.Upper));
        script.Import("large_number", new Func<decimal, string>(value => Fmt.LargeNumber(value)));

        var context = new TemplateContext { MemberRenamer = member => member.Name };
        context.PushGlobal(script);

        return template.Render(context);
    }
}
