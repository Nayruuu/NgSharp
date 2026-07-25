using BenchmarkDotNet.Attributes;

using NgSharp.Benchmark.Realistic;

namespace NgSharp.Benchmark;

// Realistic-document comparison across three anonymized archetypes that span the real ProxAffiche
// print/PDF profiles — all four engines render byte-identical output (custom filters delegate to the
// shared Fmt), so this measures engine parse+render only, not formatting:
//   Devis  (~31 KB) — pipe-heavy: 72 line items, ~400 number/date/upper/largeNumber calls, nested loops.
//   Fiche  (~4.5 KB) — conditional-heavy: ~40 [if] on bool attributes, few loops.
//   Cartes (~17 KB) — tabular: one flat 40-item loop, interpolation-heavy, few pipes.
// Cold = parse + render each call; Warm = template prepared once, render only.
[MemoryDiagnoser]
[WarmupCount(5)]
[IterationCount(10)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class RealisticDocumentBenchmark
{
    [GlobalSetup]
    public void Setup()
    {
        // Prime JIT + each engine's warm (pre-parsed) state for every archetype.
        RealisticEngines.NgSharp_Devis_Warm();
        DevisFluid.Warm();
        DevisHandlebars.Warm();
        DevisScriban.Warm();
        RealisticEngines.NgSharp_Fiche_Warm();
        FicheFluid.Warm();
        FicheHandlebars.Warm();
        FicheScriban.Warm();
        RealisticEngines.NgSharp_Cartes_Warm();
        CartesFluid.Warm();
        CartesHandlebars.Warm();
        CartesScriban.Warm();
    }

    [BenchmarkCategory("DevisCold"), Benchmark(Baseline = true)]
    public string Ng_DevisCold() => RealisticEngines.NgSharp_Devis_Cold();

    [BenchmarkCategory("DevisCold"), Benchmark]
    public object Ng_DevisParseOnly() => RealisticEngines.NgSharp_Devis_ParseOnly();   // Cold - this = render portion

    // No JsonElement row by design: the realistic benchmarks are FromObject-only (every engine gets the
    // same CLR model, apples-to-apples); the FromJson prod path is gated for correctness via realistic-verify.
    [BenchmarkCategory("DevisCold"), Benchmark]
    public string Fl_DevisCold() => DevisFluid.Cold();

    [BenchmarkCategory("DevisCold"), Benchmark]
    public string Hb_DevisCold() => DevisHandlebars.Cold();

    [BenchmarkCategory("DevisCold"), Benchmark]
    public string Sc_DevisCold() => DevisScriban.Cold();

    [BenchmarkCategory("DevisWarm"), Benchmark(Baseline = true)]
    public string Ng_DevisWarm() => RealisticEngines.NgSharp_Devis_Warm();

    [BenchmarkCategory("DevisWarm"), Benchmark]
    public string Ng_DevisRenderOnly() => RealisticEngines.NgSharp_Devis_RenderOnly();   // Warm - this = FromObject build cost

    [BenchmarkCategory("DevisWarm"), Benchmark]
    public string Fl_DevisWarm() => DevisFluid.Warm();

    [BenchmarkCategory("DevisWarm"), Benchmark]
    public string Hb_DevisWarm() => DevisHandlebars.Warm();

    [BenchmarkCategory("DevisWarm"), Benchmark]
    public string Sc_DevisWarm() => DevisScriban.Warm();

    [BenchmarkCategory("FicheCold"), Benchmark(Baseline = true)]
    public string Ng_FicheCold() => RealisticEngines.NgSharp_Fiche_Cold();

    [BenchmarkCategory("FicheCold"), Benchmark]
    public object Ng_FicheParseOnly() => RealisticEngines.NgSharp_Fiche_ParseOnly();     // cold decomposition

    [BenchmarkCategory("FicheCold"), Benchmark]
    public string Fl_FicheCold() => FicheFluid.Cold();

    [BenchmarkCategory("FicheCold"), Benchmark]
    public string Hb_FicheCold() => FicheHandlebars.Cold();

    [BenchmarkCategory("FicheCold"), Benchmark]
    public string Sc_FicheCold() => FicheScriban.Cold();

    [BenchmarkCategory("FicheWarm"), Benchmark(Baseline = true)]
    public string Ng_FicheWarm() => RealisticEngines.NgSharp_Fiche_Warm();

    [BenchmarkCategory("FicheWarm"), Benchmark]
    public string Fl_FicheWarm() => FicheFluid.Warm();

    [BenchmarkCategory("FicheWarm"), Benchmark]
    public string Hb_FicheWarm() => FicheHandlebars.Warm();

    [BenchmarkCategory("FicheWarm"), Benchmark]
    public string Sc_FicheWarm() => FicheScriban.Warm();

    [BenchmarkCategory("CartesCold"), Benchmark(Baseline = true)]
    public string Ng_CartesCold() => RealisticEngines.NgSharp_Cartes_Cold();

    [BenchmarkCategory("CartesCold"), Benchmark]
    public string Fl_CartesCold() => CartesFluid.Cold();

    [BenchmarkCategory("CartesCold"), Benchmark]
    public string Hb_CartesCold() => CartesHandlebars.Cold();

    [BenchmarkCategory("CartesCold"), Benchmark]
    public string Sc_CartesCold() => CartesScriban.Cold();

    [BenchmarkCategory("CartesWarm"), Benchmark(Baseline = true)]
    public string Ng_CartesWarm() => RealisticEngines.NgSharp_Cartes_Warm();

    [BenchmarkCategory("CartesWarm"), Benchmark]
    public string Fl_CartesWarm() => CartesFluid.Warm();

    [BenchmarkCategory("CartesWarm"), Benchmark]
    public string Hb_CartesWarm() => CartesHandlebars.Warm();

    [BenchmarkCategory("CartesWarm"), Benchmark]
    public string Sc_CartesWarm() => CartesScriban.Warm();
}
