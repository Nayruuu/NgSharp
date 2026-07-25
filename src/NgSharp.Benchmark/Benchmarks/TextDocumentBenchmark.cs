using BenchmarkDotNet.Attributes;

using NgSharp.Benchmark.Realistic;

namespace NgSharp.Benchmark;

// The TEXT-mode arena: a realistic JSON export of the devis model (nested @for over sections/lines,
// conditional discount properties, json/date literal pipes — strict-valid JSON output, ~6 KB), rendered
// byte-identically by NgSharp (TemplateMode.Text) vs Fluid / Handlebars / Scriban — the native terrain
// of the text engines, with none of NgSharp's HTML machinery in play. All JSON literal formatting
// delegates to the shared JsonFmt (gate: `textcmp`), so this measures engine parse+render only.
// Cold = parse + render each call; Warm = template prepared once, render only.
[MemoryDiagnoser]
[WarmupCount(5)]
[IterationCount(10)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class TextDocumentBenchmark
{
    [GlobalSetup]
    public void Setup()
    {
        // Prime JIT + each engine's warm (pre-parsed) state.
        RealisticEngines.NgSharp_Export_Warm();
        ExportFluid.Warm();
        ExportHandlebars.Warm();
        ExportScriban.Warm();
    }

    [BenchmarkCategory("ExportCold"), Benchmark(Baseline = true)]
    public string Ng_ExportCold() => RealisticEngines.NgSharp_Export_Cold();

    [BenchmarkCategory("ExportCold"), Benchmark]
    public string Fl_ExportCold() => ExportFluid.Cold();

    [BenchmarkCategory("ExportCold"), Benchmark]
    public string Hb_ExportCold() => ExportHandlebars.Cold();

    [BenchmarkCategory("ExportCold"), Benchmark]
    public string Sc_ExportCold() => ExportScriban.Cold();

    [BenchmarkCategory("ExportWarm"), Benchmark(Baseline = true)]
    public string Ng_ExportWarm() => RealisticEngines.NgSharp_Export_Warm();

    [BenchmarkCategory("ExportWarm"), Benchmark]
    public string Fl_ExportWarm() => ExportFluid.Warm();

    [BenchmarkCategory("ExportWarm"), Benchmark]
    public string Hb_ExportWarm() => ExportHandlebars.Warm();

    [BenchmarkCategory("ExportWarm"), Benchmark]
    public string Sc_ExportWarm() => ExportScriban.Warm();
}
