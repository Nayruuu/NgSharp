using System;
using System.Collections.Generic;

using NgSharp;
using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Parsing;
using NgSharp.Directives;
using NgSharp.Components;

using Fluid;
using RazorLight;
using HandlebarsDotNet;
using Stubble.Core.Builders;

namespace NgSharp.Benchmark;

// Same logical document in each engine's dialect, producing the same rendered content: a catalogue
// page — title + product count, then nested loops (categories, each with its products), each product
// rendering several fields plus two conditionals (in-stock / on-sale). Modelled on 8 categories of
// 12 products = 96 items, so it exercises nesting and volume, not a toy one-liner.
public static class Engines
{
    // The same logical catalog template in each engine's dialect, kept as files under Templates/
    // (embedded — see the .csproj). Loaded once here so the benchmark measures rendering, not I/O.
    public static readonly string NgSharpTpl = LoadTemplate("NgSharp/catalog.html");
    public static readonly string RazorTpl = LoadTemplate("Razor/catalog.cshtml");
    public static readonly string HandlebarsTpl = LoadTemplate("Handlebars/catalog.hbs");
    public static readonly string ScribanTpl = LoadTemplate("Scriban/catalog.scriban");
    public static readonly string MustacheTpl = LoadTemplate("Mustache/catalog.mustache");
    public static readonly string LiquidTpl = LoadTemplate("Fluid/catalog.liquid");

    // Shared engine instances, built once — Cold measures per-call parse/compile, not instance construction.
    private static readonly RazorLightEngine Razor = new RazorLightEngineBuilder()
        .UseEmbeddedResourcesProject(typeof(Engines))
        .UseMemoryCachingProvider()
        .Build();

    private static readonly IHandlebars Handlebars = HandlebarsDotNet.Handlebars.Create();

    private static readonly FluidParser Fluid = new FluidParser();

    private static readonly Stubble.Core.StubbleVisitorRenderer Stubble = new StubbleBuilder().Build();

    private static readonly global::Fluid.TemplateOptions FluidOptions = BuildFluidOptions();

    private static readonly CompiledTemplate NgCompiled = HtmlBuilder.Create().Compile(NgSharpTpl);

    // One builder, built once — parity with Fluid's static FluidParser/TemplateOptions above. Cold still
    // re-parses the template per call; it just doesn't rebuild the pipe registry (Default is new() per access).
    private static readonly HtmlBuilder NgBuilder = HtmlBuilder.Create();

    public static PageModel Model()
    {
        var categories = new List<Category>();
        var total = 0;

        for (var c = 0; c < 8; c++)
        {
            var products = new List<Product>();

            for (var p = 0; p < 12; p++)
            {
                products.Add(new Product
                {
                    Name = "Product " + c + "-" + p,
                    Sku = "SKU-" + c + p.ToString("D2"),
                    Price = 10 + p * 3,
                    InStock = p % 3 != 0,
                    OnSale = p % 4 == 0,
                    Rating = 1 + p % 5
                });
            }

            total += products.Count;
            categories.Add(new Category { Name = "Category " + c, Count = products.Count, Products = products });
        }

        return new PageModel { Title = "Product Catalogue", TotalProducts = total, Categories = categories };
    }

    public static string NgSharp_Cold(PageModel model)
        => NgBuilder.BuildFromTemplate(NgSharpTpl, model);

    // Warm = compiled once (static skeleton folded), model converted (FromObject) + rendered each call.
    public static string NgSharp_Warm(PageModel model) => NgCompiled.Render(model);

    // The model -> NgElement conversion the object overload does per render; isolated for the alloc probe.
    public static NgElement BuildContext(PageModel model) => NgElement.FromObject(model);

    // Render-only against a prebuilt context — isolates the render stage from the model->tree build.
    public static string NgSharp_RenderOnly(NgElement context)
        => NgCompiled.Render(context);

    // Parse-only (cold-stage cost isolation); returns object since the AST type is internal.
    public static object NgSharp_ParseOnly()
        => TemplateParser.ParseDocument(NgSharpTpl);   // the fused parse folds inline

    public static string Razor_Cold(PageModel model)
        // Fresh cache key => forces a fresh Roslyn compile each time (cold path).
        => Razor.CompileRenderStringAsync(Guid.NewGuid().ToString(), RazorTpl, model).GetAwaiter().GetResult();

    public static string Razor_Warm(PageModel model)
        // Stable key => compiled once, cached afterwards.
        => Razor.CompileRenderStringAsync("razor-warm", RazorTpl, model).GetAwaiter().GetResult();

    public static string Handlebars_Cold(PageModel model)
        => (string)Handlebars.Compile(HandlebarsTpl)(model);

    public static HandlebarsTemplate<object, object> Handlebars_Compile()
        => Handlebars.Compile(HandlebarsTpl);

    public static string Handlebars_Warm(HandlebarsTemplate<object, object> compiled, PageModel model)
        => (string)compiled(model);

    public static string Scriban_Cold(PageModel model)
        => Scriban.Template.Parse(ScribanTpl).Render(model, member => member.Name);

    public static Scriban.Template Scriban_Parse()
        => Scriban.Template.Parse(ScribanTpl);

    public static string Scriban_Warm(Scriban.Template template, PageModel model)
        => template.Render(model, member => member.Name);

    public static string Stubble_Cold(PageModel model)
        => Stubble.Render(MustacheTpl, model);

    public static string Fluid_Cold(PageModel model)
    {
        Fluid.TryParse(LiquidTpl, out var template, out _);

        return template.Render(new TemplateContext(model, FluidOptions));
    }

    public static IFluidTemplate Fluid_Parse()
    {
        Fluid.TryParse(LiquidTpl, out var template, out _);

        return template;
    }

    public static string Fluid_Warm(IFluidTemplate template, PageModel model)
        => template.Render(new TemplateContext(model, FluidOptions));

    // templatePath is the engine-qualified path under Templates/ (e.g. "NgSharp/catalog.html");
    // folder separators become '.' in the manifest resource name.
    internal static string LoadTemplate(string templatePath)
    {
        var assembly = typeof(Engines).Assembly;
        var suffix = "Templates." + templatePath.Replace('/', '.');
        var resource = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resource is null)
        {
            throw new InvalidOperationException($"Embedded benchmark template 'Templates/{templatePath}' not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resource);
        using var reader = new System.IO.StreamReader(stream);

        // Templates are authored multi-line; collapse to the canonical single-line form (trim each line,
        // join with no separator) so every engine receives identical whitespace and renders byte-identical
        // output. Meaningful spaces always live within a single line, so trimming never strips content.
        var raw = reader.ReadToEnd();

        return string.Concat(raw.Split('\n').Select(line => line.Trim()));
    }

    private static global::Fluid.TemplateOptions BuildFluidOptions()
    {
        var options = new global::Fluid.TemplateOptions();
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();

        return options;
    }
}
