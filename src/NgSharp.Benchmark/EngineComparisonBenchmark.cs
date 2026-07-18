using BenchmarkDotNet.Attributes;

using NgSharp;

namespace NgSharp.Benchmark
{
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
        private PageModel model;
        private NgElement ngContext;
        private HandlebarsDotNet.HandlebarsTemplate<object, object> handlebars;
        private Scriban.Template scriban;
        private Fluid.IFluidTemplate fluid;

        [GlobalSetup]
        public void Setup()
        {
            model = Engines.Model();
            ngContext = Engines.BuildContext(model);
            handlebars = Engines.Handlebars_Compile();
            scriban = Engines.Scriban_Parse();
            fluid = Engines.Fluid_Parse();

            Engines.Razor_Warm(model);   // prime the RazorLight compiled-template cache
            Engines.NgSharp_Warm(model);  // JIT warm-up
        }

        // ---------------- Cold: from scratch each render ----------------
        [BenchmarkCategory("Cold"), Benchmark(Baseline = true)]
        public string NgSharp_Cold() => Engines.NgSharp_Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string RazorLight_Cold() => Engines.Razor_Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string Handlebars_Cold() => Engines.Handlebars_Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string Scriban_Cold() => Engines.Scriban_Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string Stubble_Cold() => Engines.Stubble_Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string Fluid_Cold() => Engines.Fluid_Cold(model);

        // ---------------- Warm: template prepared once, render only ----------------
        [BenchmarkCategory("Warm"), Benchmark(Baseline = true)]
        public string NgSharp_Warm() => Engines.NgSharp_Warm(model);

        // Same as NgSharp_Warm but the NgElement is built once: (NgSharp_Warm - this) = model round-trip cost.
        [BenchmarkCategory("Warm"), Benchmark]
        public string NgSharp_Warm_Prebuilt() => Engines.NgSharp_Warm_Prebuilt(ngContext);

        [BenchmarkCategory("Warm"), Benchmark]
        public string RazorLight_Warm() => Engines.Razor_Warm(model);

        [BenchmarkCategory("Warm"), Benchmark]
        public string Handlebars_Warm() => Engines.Handlebars_Warm(handlebars, model);

        [BenchmarkCategory("Warm"), Benchmark]
        public string Scriban_Warm() => Engines.Scriban_Warm(scriban, model);

        [BenchmarkCategory("Warm"), Benchmark]
        public string Fluid_Warm() => Engines.Fluid_Warm(fluid, model);
    }
}
