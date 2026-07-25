using BenchmarkDotNet.Attributes;

using NgSharp;

namespace NgSharp.Benchmark;

// Honest engine comparison. Each method renders EXACTLY ONE template (no OperationsPerInvoke
// fudge). Two regimes:
//   Cold = build the template representation from scratch + render (first-use latency; the
//          compiled engines pay Roslyn/IL codegen here).
//   Warm = template parsed/compiled once in [GlobalSetup], benchmark measures render() only
//          (steady-state throughput). Stubble.Core has no compile step, so it appears in Cold only.
[MemoryDiagnoser]
[WarmupCount(5)]
[IterationCount(10)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class EngineComparisonBenchmark
{
    private PageModel _model;
    private HandlebarsDotNet.HandlebarsTemplate<object, object> _handlebars;
    private Scriban.Template _scriban;
    private Fluid.IFluidTemplate _fluid;

    [GlobalSetup]
    public void Setup()
    {
        _model = Engines.Model();
        _handlebars = Engines.Handlebars_Compile();
        _scriban = Engines.Scriban_Parse();
        _fluid = Engines.Fluid_Parse();

        Engines.Razor_Warm(_model);   // prime the RazorLight compiled-template cache
        Engines.NgSharp_Warm(_model);  // JIT warm-up
    }

    [BenchmarkCategory("Cold"), Benchmark(Baseline = true)]
    public string NgSharp_Cold() => Engines.NgSharp_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string RazorLight_Cold() => Engines.Razor_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string Handlebars_Cold() => Engines.Handlebars_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string Scriban_Cold() => Engines.Scriban_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string Stubble_Cold() => Engines.Stubble_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string Fluid_Cold() => Engines.Fluid_Cold(_model);

    [BenchmarkCategory("Warm"), Benchmark(Baseline = true)]
    public string NgSharp_Warm() => Engines.NgSharp_Warm(_model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string RazorLight_Warm() => Engines.Razor_Warm(_model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string Handlebars_Warm() => Engines.Handlebars_Warm(_handlebars, _model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string Scriban_Warm() => Engines.Scriban_Warm(_scriban, _model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string Fluid_Warm() => Engines.Fluid_Warm(_fluid, _model);
}
