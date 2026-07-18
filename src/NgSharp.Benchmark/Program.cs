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

if (args.Length > 0 && args[0] == "showsmoke")
{
    // Prints the feature-showcase render so every construct can be eyeballed before benchmarking.
    var company = Showcase.Model();
    try
    {
        Console.WriteLine(Showcase.Cold(company));
    }
    catch (Exception e)
    {
        Console.WriteLine($"THROW {e.GetType().Name}: {e.Message}");
    }
    return;
}

if (args.Length > 0 && args[0] == "showcmp")
{
    // Byte-identity check: NgSharp vs Handlebars on the feature-showcase document.
    var company = Showcase.Model();
    var ng = Showcase.Cold(company);

    void Diff(string name, string other)
    {
        Console.WriteLine($"NgSharp len={ng.Length}  {name} len={other.Length}  identical={ng == other}");
        if (ng == other) return;
        var min = Math.Min(ng.Length, other.Length);
        var i = 0;
        while (i < min && ng[i] == other[i]) i++;
        var from = Math.Max(0, i - 50);
        Console.WriteLine($"  first diff at index {i}:");
        Console.WriteLine($"  NG : …{ng.Substring(from, Math.Min(140, ng.Length - from))}…");
        Console.WriteLine($"  {name,-3}: …{other.Substring(from, Math.Min(140, other.Length - from))}…");
    }

    Diff("HB", Showcase.Handlebars_Warm(company));
    Diff("RZ", Showcase.RazorLight_Warm(company));
    return;
}

// Both benchmark suites are first-class: `dotnet run` runs all; target one with e.g.
// `dotnet run -c Release -- --filter *Showcase*` or `--filter *Engine*`.
BenchmarkSwitcher
    .FromTypes(new[] { typeof(EngineComparisonBenchmark), typeof(FeatureShowcaseBenchmark) })
    .Run(args.Length == 0 ? new[] { "--filter", "*" } : args);
