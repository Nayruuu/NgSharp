using System;
using System.Linq;

namespace NgSharp.Benchmark;

internal static class ProgramModes
{
    public static void Smoke()
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
    }

    public static void RealisticVerify()
    {
        // Byte-identity gate against the COMMITTED goldens (Realistic/Expected/*): every engine × every
        // archetype + NgSharp's JSON ingestion path must match exactly. Exit code 1 on any mismatch.
        var asm = typeof(NgSharp.Benchmark.Realistic.RealisticEngines).Assembly;

        string Golden(string doc)
        {
            var resource = Array.Find(asm.GetManifestResourceNames(), name => name.EndsWith($"Realistic.Expected.{doc}.expected.html", StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"golden '{doc}' not embedded");
            using var stream = asm.GetManifestResourceStream(resource);
            using var reader = new System.IO.StreamReader(stream);

            return reader.ReadToEnd();
        }

        var failures = 0;

        void Check(string label, string expected, Func<string> render)
        {
            string actual;
            try { actual = render(); }
            catch (Exception e) { Console.WriteLine($"❌ {label}: THROW {e.GetType().Name}: {e.Message}"); failures++; return; }
            if (actual == expected) { Console.WriteLine($"✅ {label}"); }
            else { Console.WriteLine($"❌ {label}: len={actual.Length} vs {expected.Length}"); failures++; }
        }

        var devis = Golden("devis"); var fiche = Golden("fiche"); var cartes = Golden("cartes");
        Check("devis  ngsharp    ", devis, NgSharp.Benchmark.Realistic.RealisticEngines.NgSharp_Devis_Cold);
        Check("devis  ngsharp-json", devis, NgSharp.Benchmark.Realistic.RealisticEngines.NgSharp_Devis_ColdJson);
        Check("devis  fluid      ", devis, NgSharp.Benchmark.Realistic.DevisFluid.Cold);
        Check("devis  handlebars ", devis, NgSharp.Benchmark.Realistic.DevisHandlebars.Cold);
        Check("devis  scriban    ", devis, NgSharp.Benchmark.Realistic.DevisScriban.Cold);
        Check("fiche  ngsharp    ", fiche, NgSharp.Benchmark.Realistic.RealisticEngines.NgSharp_Fiche_Cold);
        Check("fiche  fluid      ", fiche, NgSharp.Benchmark.Realistic.FicheFluid.Cold);
        Check("fiche  handlebars ", fiche, NgSharp.Benchmark.Realistic.FicheHandlebars.Cold);
        Check("fiche  scriban    ", fiche, NgSharp.Benchmark.Realistic.FicheScriban.Cold);
        Check("cartes ngsharp    ", cartes, NgSharp.Benchmark.Realistic.RealisticEngines.NgSharp_Cartes_Cold);
        Check("cartes fluid      ", cartes, NgSharp.Benchmark.Realistic.CartesFluid.Cold);
        Check("cartes handlebars ", cartes, NgSharp.Benchmark.Realistic.CartesHandlebars.Cold);
        Check("cartes scriban    ", cartes, NgSharp.Benchmark.Realistic.CartesScriban.Cold);

        Console.WriteLine(failures == 0 ? "ALL GOLDEN" : $"{failures} MISMATCH(ES)");
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    public static void RealisticTime(string[] args)
    {
        // In-process comparative timing: warms up then times each named RealisticEngines method back-to-back,
        // so machine contention hits all candidates equally (useful when BenchmarkDotNet runs are noisy).
        var iterations = int.Parse(args[1]);
        var names = args[2..];
        var methods = Array.ConvertAll(names, name =>
            typeof(NgSharp.Benchmark.Realistic.RealisticEngines).GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new ArgumentException($"method {name} not found"));

        // Warmup + JIT.
        foreach (var method in methods)
        {
            for (var w = 0; w < 30; w++)
            {
                method.Invoke(null, null);
            }
        }

        // Interleave in small chunks (A,B,C, A,B,C, ...) so GC/LOH ramp-up and machine drift hit every candidate
        // equally — sequential per-method blocks hand the FIRST one the whole warm-up bill (measured: ~40% phantom gap).
        var chunk = 50;
        var rounds = Math.Max(1, iterations / chunk);
        var totals = new double[methods.Length];

        for (var r = 0; r < rounds; r++)
        {
            for (var mi = 0; mi < methods.Length; mi++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < chunk; i++)
                {
                    methods[mi].Invoke(null, null);
                }

                sw.Stop();
                totals[mi] += sw.Elapsed.TotalMicroseconds;
            }
        }

        for (var mi = 0; mi < methods.Length; mi++)
        {
            Console.WriteLine($"{methods[mi].Name,-28} {totals[mi] / (rounds * chunk),10:F1} µs/op");
        }
    }

    public static void RealisticRender(string[] args)
    {
        // Generic: realistic-render <TypeName> <Method> [outPath] — reflectively renders an engine port's output
        // (e.g. DevisFluid Cold) so each port can be diffed against the NgSharp reference without touching Program.
        var asm = typeof(NgSharp.Benchmark.Realistic.RealisticEngines).Assembly;
        var type = asm.GetType("NgSharp.Benchmark.Realistic." + args[1])
            ?? throw new ArgumentException($"type NgSharp.Benchmark.Realistic.{args[1]} not found");
        var method = type.GetMethod(args[2], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new ArgumentException($"static method {args[1]}.{args[2]} not found");
        var html = (string)method.Invoke(null, null);

        if (args.Length > 3)
        {
            System.IO.File.WriteAllText(args[3], html);
            Console.Error.WriteLine($"{args[1]}.{args[2]}: LEN={html.Length} written={args[3]}");
        }
        else
        {
            Console.Error.WriteLine($"{args[1]}.{args[2]}: LEN={html.Length}");
            Console.Write(html);
        }
    }

    public static void EngineCmp()
    {
        // Verifies every engine renders BYTE-IDENTICAL output to NgSharp on the Engines catalog model
        // (nested loops + conditionals) — i.e. that the benchmark compares like-for-like work.
        var model = Engines.Model();
        var ng = Engines.NgSharp_Cold(model);

        void Cmp(string name, Func<string> render)
        {
            try
            {
                var other = render();
                Console.WriteLine($"NgSharp len={ng.Length}  {name,-11} len={other.Length}  identical={ng == other}");
                if (ng != other)
                {
                    var min = Math.Min(ng.Length, other.Length);
                    var i = 0;
                    while (i < min && ng[i] == other[i])
                    {
                        i++;
                    }

                    var from = Math.Max(0, i - 30);
                    Console.WriteLine($"  first diff @ {i}:");
                    Console.WriteLine($"  NG : …{ng.Substring(from, Math.Min(90, ng.Length - from))}…");
                    Console.WriteLine($"  {name}: …{other.Substring(Math.Min(from, other.Length), Math.Min(90, Math.Max(0, other.Length - from)))}…");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{name}: THROW {e.GetType().Name}: {e.Message}");
            }
        }

        Cmp("Fluid", () => Engines.Fluid_Cold(model));
        Cmp("Handlebars", () => Engines.Handlebars_Cold(model));
        Cmp("RazorLight", () => Engines.Razor_Cold(model));
        Cmp("Scriban", () => Engines.Scriban_Cold(model));
        Cmp("Stubble", () => Engines.Stubble_Cold(model));
    }

    public static void Alloc()
    {
        // Attribute NgSharp's per-render allocation + rough time across the three stages: parse (cold only),
        // model->tree (FromObject), and render. Isolates where the gap vs Fluid actually lives.
        var model = Engines.Model();

        // Warm up JIT + static caches so measurements reflect steady state, not first-use.
        for (var i = 0; i < 200; i++)
        {
            var ctxw = Engines.BuildContext(model);
            _ = Engines.NgSharp_RenderOnly(ctxw);
            _ = Engines.NgSharp_ParseOnly();
        }

        (long bytes, double us) Measure(Action body, int reps)
        {
            body();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < reps; i++)
            {
                body();
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            sw.Stop();

            return ((after - before) / reps, sw.Elapsed.TotalMicroseconds / reps);
        }

        var ctx = Engines.BuildContext(model);
        var parse = Measure(() => Engines.NgSharp_ParseOnly(), 2000);
        var build = Measure(() => Engines.BuildContext(model), 2000);
        var render = Measure(() => Engines.NgSharp_RenderOnly(ctx), 2000);

        Console.WriteLine($"parse (cold only) : {parse.bytes,7} B   {parse.us,7:F2} us");
        Console.WriteLine($"build (FromObject): {build.bytes,7} B   {build.us,7:F2} us");
        Console.WriteLine($"render            : {render.bytes,7} B   {render.us,7:F2} us");
        Console.WriteLine($"---");
        Console.WriteLine($"warm  (build+render): {build.bytes + render.bytes,7} B   {build.us + render.us,7:F2} us");
        Console.WriteLine($"cold  (all three)   : {parse.bytes + build.bytes + render.bytes,7} B   {parse.us + build.us + render.us,7:F2} us");
        Console.WriteLine($"output length: {Engines.NgSharp_RenderOnly(ctx).Length} chars");
    }

    public static void ParseAlloc()
    {
        // Decomposes the fiche cold-PARSE allocation: the same byte-for-byte template with its dynamic
        // constructs neutralized isolates the static machinery (const runs, nodes, tag scanning) from the
        // expression/binding cost. Deterministic — load-immune.
        var fiche = NgSharp.Benchmark.Realistic.RealisticEngines.FicheNg;
        var neutral = fiche.Replace("{{", "((").Replace("}}", "))").Replace("[if]", "zif").Replace("[for]", "zfor").Replace("[not-empty]", "znot-empty").Replace("@if", "xif").Replace("@for", "xfor");

        (long bytes, double us) Measure(Func<object> body, int reps)
        {
            for (var w = 0; w < 200; w++)
            {
                body();
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < reps; i++)
            {
                body();
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            sw.Stop();

            return ((after - before) / reps, sw.Elapsed.TotalMicroseconds / reps);
        }

        var noInterp = fiche.Replace("{{", "((").Replace("}}", "))");
        var noBindings = fiche.Replace("[if]", "zif").Replace("[for]", "zfor").Replace("[not-empty]", "znot-empty").Replace("@if", "xif").Replace("@for", "xfor");

        var full = Measure(() => NgSharp.Parsing.TemplateParser.ParseDocument(fiche), 3000);
        var stat = Measure(() => NgSharp.Parsing.TemplateParser.ParseDocument(neutral), 3000);
        var interpOnly = Measure(() => NgSharp.Parsing.TemplateParser.ParseDocument(noBindings), 3000);
        var bindingsOnly = Measure(() => NgSharp.Parsing.TemplateParser.ParseDocument(noInterp), 3000);

        Console.WriteLine($"template chars     : {fiche.Length}");
        Console.WriteLine($"parse FULL         : {full.bytes,7} B   {full.us,7:F2} us");
        Console.WriteLine($"parse STATIC-ONLY  : {stat.bytes,7} B   {stat.us,7:F2} us");
        Console.WriteLine($"parse INTERP-ONLY  : {interpOnly.bytes,7} B (interp cost = {interpOnly.bytes - stat.bytes} B)");
        Console.WriteLine($"parse BINDINGS-ONLY: {bindingsOnly.bytes,7} B (binding cost = {bindingsOnly.bytes - stat.bytes} B)");
        Console.WriteLine($"dynamics (delta)   : {full.bytes - stat.bytes,7} B");
    }

    public static void ShowSmoke()
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
    }

    public static void ShowCmp()
    {
        // Byte-identity check: NgSharp vs Handlebars / RazorLight / Fluid on the feature-showcase document.
        var company = Showcase.Model();
        var ng = Showcase.Cold(company);

        void Diff(string name, string other)
        {
            Console.WriteLine($"NgSharp len={ng.Length}  {name} len={other.Length}  identical={ng == other}");
            if (ng == other)
            {
                return;
            }

            var min = Math.Min(ng.Length, other.Length);
            var i = 0;
            while (i < min && ng[i] == other[i])
            {
                i++;
            }

            var from = Math.Max(0, i - 50);
            Console.WriteLine($"  first diff at index {i}:");
            Console.WriteLine($"  NG : …{ng.Substring(from, Math.Min(140, ng.Length - from))}…");
            Console.WriteLine($"  {name,-3}: …{other.Substring(from, Math.Min(140, other.Length - from))}…");
        }

        Diff("HB", Showcase.Handlebars_Warm(company));
        Diff("RZ", Showcase.RazorLight_Warm(company));
        Diff("FL", Showcase.Fluid_Warm(company));
    }

    public static void TextCmp()
    {
        // The TEXT-mode arena gate: NgSharp (TemplateMode.Text) vs Fluid / Handlebars / Scriban on the
        // JSON export of the devis model — byte-identity across the four engines (showcmp pattern) plus
        // a strict JsonDocument.Parse on the NgSharp output (the document must be VALID JSON).
        // Exit code 1 on any mismatch or parse failure.
        var ng = NgSharp.Benchmark.Realistic.RealisticEngines.NgSharp_Export_Cold();
        var failures = 0;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(ng);
            Console.WriteLine($"NgSharp JSON: VALID ({ng.Length} chars, root has {document.RootElement.EnumerateObject().Count()} properties)");
        }
        catch (System.Text.Json.JsonException e)
        {
            Console.WriteLine($"NgSharp JSON: INVALID — {e.Message}");
            failures++;
        }

        void Diff(string name, Func<string> render)
        {
            string other;
            try { other = render(); }
            catch (Exception e) { Console.WriteLine($"{name}: THROW {e.GetType().Name}: {e.Message}"); failures++; return; }

            Console.WriteLine($"NgSharp len={ng.Length}  {name} len={other.Length}  identical={ng == other}");
            if (ng == other)
            {
                return;
            }

            failures++;
            var min = Math.Min(ng.Length, other.Length);
            var i = 0;
            while (i < min && ng[i] == other[i])
            {
                i++;
            }

            var from = Math.Max(0, i - 50);
            Console.WriteLine($"  first diff at index {i}:");
            Console.WriteLine($"  NG : …{ng.Substring(from, Math.Min(140, ng.Length - from))}…");
            Console.WriteLine($"  {name,-3}: …{other.Substring(Math.Min(from, other.Length), Math.Min(140, Math.Max(0, other.Length - Math.Min(from, other.Length))))}…");
        }

        Diff("FL", NgSharp.Benchmark.Realistic.ExportFluid.Cold);
        Diff("HB", NgSharp.Benchmark.Realistic.ExportHandlebars.Cold);
        Diff("SC", NgSharp.Benchmark.Realistic.ExportScriban.Cold);

        if (failures > 0)
        {
            Environment.Exit(1);
        }
    }
}
