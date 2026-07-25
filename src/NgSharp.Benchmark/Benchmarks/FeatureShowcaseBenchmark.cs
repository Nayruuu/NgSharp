using BenchmarkDotNet.Attributes;

using NgSharp;

namespace NgSharp.Benchmark;

// Feature-complete document (see Showcase.cs), NgSharp vs Handlebars.Net / RazorLight / Fluid — each
// port needs custom helpers/filters (or full C#) to reach the byte-identical output NgSharp gets
// natively. Cold = build from scratch; Warm = prepared once, render only; NgSharp_Warm_Prebuilt reuses
// the NgElement context.
[MemoryDiagnoser]
[WarmupCount(5)]
[IterationCount(10)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class FeatureShowcaseBenchmark
{
    private Company _model;
    private NgElement _context;

    [GlobalSetup]
    public void Setup()
    {
        _model = Showcase.Model();
        _context = Showcase.Context(_model);

        Showcase.Warm(_model);            // JIT warm-up
        Showcase.Handlebars_Warm(_model); // prime the compiled Handlebars template
        Showcase.RazorLight_Warm(_model); // prime the compiled Razor template
        Showcase.Fluid_Warm(_model);      // prime the parsed Fluid template
    }

    [BenchmarkCategory("Cold"), Benchmark(Baseline = true)]
    public string NgSharp_Cold() => Showcase.Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string Handlebars_Cold() => Showcase.Handlebars_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string RazorLight_Cold() => Showcase.RazorLight_Cold(_model);

    [BenchmarkCategory("Cold"), Benchmark]
    public string Fluid_Cold() => Showcase.Fluid_Cold(_model);

    [BenchmarkCategory("Warm"), Benchmark(Baseline = true)]
    public string NgSharp_Warm() => Showcase.Warm(_model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string Handlebars_Warm() => Showcase.Handlebars_Warm(_model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string RazorLight_Warm() => Showcase.RazorLight_Warm(_model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string Fluid_Warm() => Showcase.Fluid_Warm(_model);

    [BenchmarkCategory("Warm"), Benchmark]
    public string NgSharp_Warm_Prebuilt() => Showcase.WarmPrebuilt(_context);
}
