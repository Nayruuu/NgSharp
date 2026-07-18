using System;
using System.Collections.Generic;

using NgSharp;
using NgSharp.Pipes;
using NgSharp.Template;
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

        public int Price { get; set; }

        public bool InStock { get; set; }
    }

    public class PageModel
    {
        public string Title { get; set; }

        public List<Product> Items { get; set; }
    }

    // Same logical template in each engine's dialect, producing the same rendered content:
    //   a title, then a list of products (name + price) with an "(in stock)" suffix when InStock.
    public static class Engines
    {
        public static PageModel Model() => new PageModel
        {
            Title = "Catalogue",
            Items = new List<Product>
            {
                new Product { Name = "Widget", Price = 10, InStock = true },
                new Product { Name = "Gadget", Price = 20, InStock = false },
                new Product { Name = "Gizmo", Price = 30, InStock = true }
            }
        };

        public const string NgSharpTpl =
            "<h1>{{ Title }}</h1><ul><li [for]=\"Items\">{{ Name }} - {{ Price }}€<span [if]=\"InStock == true\"> (in stock)</span></li></ul>";

        public const string RazorTpl =
            "<h1>@Model.Title</h1><ul>@foreach(var i in Model.Items){<li>@i.Name - @i.Price€@if(i.InStock){<text> (in stock)</text>}</li>}</ul>";

        public const string HandlebarsTpl =
            "<h1>{{Title}}</h1><ul>{{#each Items}}<li>{{Name}} - {{Price}}€{{#if InStock}} (in stock){{/if}}</li>{{/each}}</ul>";

        public const string ScribanTpl =
            "<h1>{{ Title }}</h1><ul>{{ for i in Items }}<li>{{ i.Name }} - {{ i.Price }}€{{ if i.InStock }} (in stock){{ end }}</li>{{ end }}</ul>";

        public const string MustacheTpl =
            "<h1>{{Title}}</h1><ul>{{#Items}}<li>{{Name}} - {{Price}}€{{#InStock}} (in stock){{/InStock}}</li>{{/Items}}</ul>";

        public const string LiquidTpl =
            "<h1>{{ Title }}</h1><ul>{% for i in Items %}<li>{{ i.Name }} - {{ i.Price }}€{% if i.InStock %} (in stock){% endif %}</li>{% endfor %}</ul>";

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
        private static readonly IReadOnlyList<TemplateNode> NgWarmNodes = TemplateParser.ParseDocument(NgSharpTpl);
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
            return HtmlBuilder.MinifyHtml(NgSharp.Template.TemplateRenderer.Render(NgWarmNodes, context, NoPipes, NoComponents, NoDirectives));
        }

        // Direct object -> NgElement, the conversion the object overload now does per render
        // (~1.8x faster / ~1.5x less allocation than the old model -> JSON -> NgElement round-trip).
        public static NgElement BuildContext(PageModel model) => NgElement.FromObject(model);

        // Warm with the NgElement built ONCE (context reused) — isolates the pure render/interpret
        // cost from the model round-trip. (Warm - this) == the JSON round-trip cost.
        public static string NgSharp_Warm_Prebuilt(NgElement context)
            => HtmlBuilder.MinifyHtml(NgSharp.Template.TemplateRenderer.Render(NgWarmNodes, context, NoPipes, NoComponents, NoDirectives));

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
