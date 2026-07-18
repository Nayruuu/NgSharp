using System;

using BenchmarkDotNet.Running;

using NgSharp.Benchmark;

if (args.Length > 0 && args[0] == "smoke")
{
    var model = Engines.Model();

    void Show(string name, Func<string> render)
    {
        try
        {
            Console.WriteLine($"{name,-12}: {render()}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"{name,-12}: THROW {e.GetType().Name}: {e.Message}");
        }
    }

    Show("NgSharp", () => Engines.NgSharp_Cold(model));
    Show("RazorLight", () => Engines.Razor_Cold(model));
    Show("Handlebars", () => Engines.Handlebars_Cold(model));
    Show("Scriban", () => Engines.Scriban_Cold(model));
    Show("Stubble", () => Engines.Stubble_Cold(model));
    Show("Fluid", () => Engines.Fluid_Cold(model));
    return;
}

BenchmarkRunner.Run<EngineComparisonBenchmark>(args: args);
