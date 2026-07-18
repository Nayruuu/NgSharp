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

namespace NgSharp.Benchmark
{
    public class Product
    {
        public string Name { get; set; }

        public string Sku { get; set; }

        public int Price { get; set; }

        public bool InStock { get; set; }

        public bool OnSale { get; set; }

        public int Rating { get; set; }
    }

    public class Category
    {
        public string Name { get; set; }

        public int Count { get; set; }

        public List<Product> Products { get; set; }
    }

    public class PageModel
    {
        public string Title { get; set; }

        public int TotalProducts { get; set; }

        public List<Category> Categories { get; set; }
    }

    // Same logical document in each engine's dialect, producing the same rendered content: a catalogue
    // page — title + product count, then nested loops (categories, each with its products), each product
    // rendering several fields plus two conditionals (in-stock / on-sale). Modelled on 8 categories of
    // 12 products = 96 items, so it exercises nesting and volume, not a toy one-liner.
    public static class Engines
    {
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

        // The same logical catalog template in each engine's dialect, kept as files under Templates/
        // (embedded — see the .csproj). Loaded once here so the benchmark measures rendering, not I/O.
        public static readonly string NgSharpTpl = LoadTemplate("ngsharp.html");
        public static readonly string RazorTpl = LoadTemplate("razor.cshtml");
        public static readonly string HandlebarsTpl = LoadTemplate("handlebars.hbs");
        public static readonly string ScribanTpl = LoadTemplate("scriban.scriban");
        public static readonly string MustacheTpl = LoadTemplate("mustache.mustache");
        public static readonly string LiquidTpl = LoadTemplate("liquid.liquid");

        internal static string LoadTemplate(string fileName)
        {
            var assembly = typeof(Engines).Assembly;
            var resource = Array.Find(
                assembly.GetManifestResourceNames(),
                name => name.EndsWith("Templates." + fileName, StringComparison.Ordinal));

            if (resource == null)
            {
                throw new InvalidOperationException($"Embedded benchmark template 'Templates/{fileName}' not found.");
            }

            using var stream = assembly.GetManifestResourceStream(resource);
            using var reader = new System.IO.StreamReader(stream);

            // The files are authored multi-line for readability; collapse to the canonical single-line
            // form — trim each line and join with no separator — so every engine receives identical
            // whitespace and renders byte-identical output. Text with meaningful spaces is always kept
            // within a single line, so trimming only ever strips indentation, never content.
            var raw = reader.ReadToEnd();
            return string.Concat(raw.Split('\n').Select(line => line.Trim()));
        }

        // ---- Shared, reusable engine instances (built once) ----
        private static readonly RazorLightEngine Razor = new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(Engines))
            .UseMemoryCachingProvider()
            .Build();

        private static readonly IHandlebars Handlebars = HandlebarsDotNet.Handlebars.Create();

        private static readonly FluidParser Fluid = new FluidParser();

        private static readonly Stubble.Core.StubbleVisitorRenderer Stubble = new StubbleBuilder().Build();

        private static readonly TemplateOptions FluidOptions = BuildFluidOptions();

        private static TemplateOptions BuildFluidOptions()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();
            return options;
        }

        // ================= NgSharp =================
        // Compiled once (static skeleton folded), exactly what HtmlBuilder.Compile does for render-many.
        private static readonly IReadOnlyList<TemplateNode> NgWarmNodes =
            NgSharp.Rendering.TemplateProgram.Compile(TemplateParser.ParseDocument(NgSharpTpl));
        private static readonly IReadOnlyDictionary<string, IPipe> NoPipes = new Dictionary<string, IPipe>();
        private static readonly IReadOnlyDictionary<string, IComponent> NoComponents = new Dictionary<string, IComponent>();
        private static readonly IReadOnlyDictionary<string, IDirective> NoDirectives = new Dictionary<string, IDirective>();

        public static string NgSharp_Cold(PageModel model)
            => HtmlBuilder.Default.BuildFromTemplateAsync(NgSharpTpl, model).GetAwaiter().GetResult();

        // Warm = AST parsed once (the AST-cache scenario NgSharp doesn't expose publicly yet).
        // Still converts the model each render via FromObject, exactly like the object overload does.
        public static string NgSharp_Warm(PageModel model)
        {
            var context = BuildContext(model);
            return NgSharp.Rendering.TemplateRenderer.Render(NgWarmNodes, context, NoPipes, NoComponents, NoDirectives);
        }

        // Direct object -> NgElement, the conversion the object overload now does per render
        // (~1.8x faster / ~1.5x less allocation than the old model -> JSON -> NgElement round-trip).
        public static NgElement BuildContext(PageModel model) => NgElement.FromObject(model);

        // ================= RazorLight (compiled) =================
        public static string Razor_Cold(PageModel model)
            // Fresh cache key => forces a fresh Roslyn compile each time (cold path).
            => Razor.CompileRenderStringAsync(Guid.NewGuid().ToString(), RazorTpl, model).GetAwaiter().GetResult();

        public static string Razor_Warm(PageModel model)
            // Stable key => compiled once, cached afterwards.
            => Razor.CompileRenderStringAsync("razor-warm", RazorTpl, model).GetAwaiter().GetResult();

        // ================= Handlebars.NET (compiled) =================
        public static string Handlebars_Cold(PageModel model)
            => (string)Handlebars.Compile(HandlebarsTpl)(model);

        public static HandlebarsTemplate<object, object> Handlebars_Compile()
            => Handlebars.Compile(HandlebarsTpl);

        public static string Handlebars_Warm(HandlebarsTemplate<object, object> compiled, PageModel model)
            => (string)compiled(model);

        // ================= Scriban (interpreted) =================
        public static string Scriban_Cold(PageModel model)
            => Scriban.Template.Parse(ScribanTpl).Render(model, member => member.Name);

        public static Scriban.Template Scriban_Parse()
            => Scriban.Template.Parse(ScribanTpl);

        public static string Scriban_Warm(Scriban.Template template, PageModel model)
            => template.Render(model, member => member.Name);

        // ================= Stubble / Mustache (interpreted) =================
        public static string Stubble_Cold(PageModel model)
            => Stubble.Render(MustacheTpl, model);

        // ================= Fluid / Liquid (interpreted) =================
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
    }
}
