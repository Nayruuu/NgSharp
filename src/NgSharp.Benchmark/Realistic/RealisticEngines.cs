using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;

using NgSharp;

namespace NgSharp.Benchmark.Realistic;

// Renders the anonymized "realistic document" templates (devis / fiche / listes-cartes) across the engines,
// for a benchmark representative of the real ProxAffiche print/PDF workload (heavy nesting, conditionals,
// number/date formatting) rather than the toy catalog. Every engine renders byte-identical output.
public static class RealisticEngines
{
    // Template sources: embedded (BDN child process), collapsed to single-line canonical whitespace like Engines.
    public static readonly string DevisNg = Load("NgSharp/devis.ngsharp.html");
    public static readonly string FicheNg = Load("NgSharp/fiche.ngsharp.html");
    public static readonly string CartesNg = Load("NgSharp/cartes.ngsharp.html");

    // The TEXT-mode arena: a JSON export of the SAME Quote model ({{ }} + @for/@if, no HTML), rendered
    // with TemplateMode.Text — the native terrain of the text engines (Fluid/Handlebars/Scriban).
    public static readonly string ExportNg = Load("NgSharp/export.json.txt");

    public static readonly Quote DevisModel = RealisticData.BuildQuote();
    public static readonly ProductSheet FicheModel = RealisticData.BuildProductSheet();
    public static readonly CardList CartesModel = RealisticData.BuildCardList();

    // The devis model as a JsonElement — the PROD ingestion path (SystemApi.Templates calls
    // BuildFromTemplate(template, JsonElement)). NOT benchmarked (benchmarks are FromObject-only,
    // apples-to-apples across engines); kept as a byte-identity CORRECTNESS gate via realistic-verify.
    public static readonly JsonElement DevisJson = JsonSerializer.SerializeToElement(DevisModel);

    // One builder, built once — parity with the harness treatment of the other engines (Fluid's
    // FluidParser/TemplateOptions are static readonly). Cold still re-parses the template per call;
    // it just doesn't rebuild the pipe registry.
    private static readonly HtmlBuilder Builder = HtmlBuilder.Create();

    private static readonly CompiledTemplate DevisNgCompiled = HtmlBuilder.Create().Compile(DevisNg);
    private static readonly CompiledTemplate FicheNgCompiled = HtmlBuilder.Create().Compile(FicheNg);
    private static readonly CompiledTemplate CartesNgCompiled = HtmlBuilder.Create().Compile(CartesNg);
    private static readonly CompiledTemplate ExportNgCompiled = HtmlBuilder.Create().Compile(ExportNg, new TemplateOptions { Mode = TemplateMode.Text });

    public static string NgSharp_Devis_Cold()
        => Builder.BuildFromTemplate(DevisNg, DevisModel);

    // The prod-shaped cold: parse + render against a JsonElement model (the SystemApi.Templates path).
    public static string NgSharp_Devis_ColdJson()
        => Builder.BuildFromTemplate(DevisNg, DevisJson);

    public static string NgSharp_Devis_Warm() => DevisNgCompiled.Render(DevisModel);

    // Render against a context built once: Warm - RenderOnly = the per-render FromObject build cost.
    private static readonly NgElement DevisContext = NgElement.FromObject(DevisModel);

    public static string NgSharp_Devis_RenderOnly() => DevisNgCompiled.Render(DevisContext);

    // Parse only (no render): Cold - ParseOnly = the render portion of the cold path.
    public static object NgSharp_Devis_ParseOnly() => NgSharp.Parsing.TemplateParser.ParseDocument(DevisNg);

    // Same-process passthrough so realistic-time can compare the true-cold paths back-to-back.
    public static string Fluid_Devis_Cold() => DevisFluid.Cold();

    public static object NgSharp_Fiche_ParseOnly() => NgSharp.Parsing.TemplateParser.ParseDocument(FicheNg);

    public static string Fluid_Fiche_Cold() => FicheFluid.Cold();

    public static string Fluid_Cartes_Cold() => CartesFluid.Cold();

    public static string NgSharp_Fiche_Cold()
        => Builder.BuildFromTemplate(FicheNg, FicheModel);

    public static string NgSharp_Fiche_Warm() => FicheNgCompiled.Render(FicheModel);

    public static string NgSharp_Cartes_Cold()
        => Builder.BuildFromTemplate(CartesNg, CartesModel);

    public static string NgSharp_Cartes_Warm() => CartesNgCompiled.Render(CartesModel);

    // The TEXT-mode arena renders the devis model as a JSON export document.
    public static string NgSharp_Export_Cold()
        => Builder.BuildFromTemplate(ExportNg, DevisModel, new TemplateOptions { Mode = TemplateMode.Text });

    public static string NgSharp_Export_Warm() => ExportNgCompiled.Render(DevisModel);

    // Same-process passthroughs so realistic-time can compare the export paths back-to-back.
    public static string Fluid_Export_Cold() => ExportFluid.Cold();

    public static string Fluid_Export_Warm() => ExportFluid.Warm();

    public static string Hb_Export_Warm() => ExportHandlebars.Warm();

    public static string Sc_Export_Warm() => ExportScriban.Warm();

    // Same-process passthroughs so realistic-time can compare the warm (pre-compiled) paths back-to-back.
    public static string Hb_Devis_Warm() => DevisHandlebars.Warm();

    public static string Hb_Fiche_Warm() => FicheHandlebars.Warm();

    public static string Hb_Cartes_Warm() => CartesHandlebars.Warm();

    // The showcase (the 87 KB feature doc): NgSharp vs the two compiled engines, same process.
    private static readonly Company ShowcaseModel = Showcase.Model();

    public static string Ng_Showcase_Warm() => Showcase.Warm(ShowcaseModel);

    public static string Hb_Showcase_Warm() => Showcase.Handlebars_Warm(ShowcaseModel);

    public static string Rz_Showcase_Warm() => Showcase.RazorLight_Warm(ShowcaseModel);

    // Isolates the per-render FromObject wrap: Warm minus this = the wrap cost.
    private static readonly NgElement ShowcaseContext = Showcase.Context(ShowcaseModel);

    public static string Ng_Showcase_WarmPrebuilt() => Showcase.WarmPrebuilt(ShowcaseContext);

    // templatePath is the engine-qualified path under Realistic/Templates/ (e.g. "Fluid/devis.fluid.liquid");
    // folder separators become '.' in the manifest resource name.
    internal static string Load(string templatePath)
    {
        var assembly = typeof(RealisticEngines).Assembly;
        var suffix = "Realistic.Templates." + templatePath.Replace('/', '.');
        var resource = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resource is null)
        {
            throw new InvalidOperationException($"Embedded realistic template '{templatePath}' not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resource);
        using var reader = new StreamReader(stream);

        return string.Concat(reader.ReadToEnd().Split('\n').Select(line => line.Trim()));
    }
}
