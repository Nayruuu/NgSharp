using BenchmarkDotNet.Attributes;

using NgSharp;

namespace NgSharp.Benchmark
{
    // Feature-complete document (see Showcase.cs), NgSharp vs Handlebars.Net — the only other engine here
    // that can express the operators / formatting / component (Handlebars needs ~9 custom helpers to reach
    // the same byte-identical output NgSharp gets natively). Cold = build from scratch; Warm = prepared
    // once, render only; NgSharp_Warm_Prebuilt reuses the NgElement context.
    [MemoryDiagnoser]
    [WarmupCount(5)]
    [IterationCount(10)]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
    public class FeatureShowcaseBenchmark
    {
        private Company model;
        private NgElement context;

        [GlobalSetup]
        public void Setup()
        {
            model = Showcase.Model();
            context = Showcase.Context(model);
            Showcase.Warm(model);            // JIT warm-up
            Showcase.Handlebars_Warm(model); // prime the compiled Handlebars template
            Showcase.RazorLight_Warm(model); // prime the compiled Razor template
        }

        [BenchmarkCategory("Cold"), Benchmark(Baseline = true)]
        public string NgSharp_Cold() => Showcase.Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string Handlebars_Cold() => Showcase.Handlebars_Cold(model);

        [BenchmarkCategory("Cold"), Benchmark]
        public string RazorLight_Cold() => Showcase.RazorLight_Cold(model);

        [BenchmarkCategory("Warm"), Benchmark(Baseline = true)]
        public string NgSharp_Warm() => Showcase.Warm(model);

        [BenchmarkCategory("Warm"), Benchmark]
        public string Handlebars_Warm() => Showcase.Handlebars_Warm(model);

        [BenchmarkCategory("Warm"), Benchmark]
        public string RazorLight_Warm() => Showcase.RazorLight_Warm(model);

        [BenchmarkCategory("Warm"), Benchmark]
        public string NgSharp_Warm_Prebuilt() => Showcase.WarmPrebuilt(context);
    }
}
