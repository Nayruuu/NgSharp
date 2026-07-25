using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using Fluid;
using Fluid.Values;
using HandlebarsDotNet;

using NgSharp;
using NgSharp.Pipes;

namespace NgSharp.Benchmark;

// A deep, feature-complete NgSharp benchmark: one document that exercises everything the engine
// does — 4 levels of nested [for], @if/@else-if/@else blocks + the [if]/[else-if]/[else] attribute
// chain, @switch/@case/@default dispatch, @for (x of ...) named loop variables, <ng-template>/@render
// fragments (two contexts), transparent ng-container, &&/||/comparisons/!=/ternary/indexers/.Count/
// .Length, [not-empty] and its dual [empty], [class.x]/[attr.x]/[style.x]/[html] bindings, rawtext
// <style> interpolation, preserved comments, all thirteen built-in pipes (date/number/currency/upper/
// lower/titlecase/default/truncate/join/pad/largeNumber/image — json is exercised by the text arena),
// a custom pipe (initials), a custom directive ([audit]) and a custom component. The Handlebars,
// RazorLight and Fluid ports below emit byte-identical output (see the showcmp gate), proving output
// equivalence rather than syntax parity.
public static class Showcase
{
    public static readonly string Template = Engines.LoadTemplate("NgSharp/showcase.html");

    private static readonly HtmlBuilder Builder = CreateBuilder();

    private static readonly CompiledTemplate Compiled = Builder.Compile(Template);

    // Handlebars.Net equivalent: same document, byte-identical output — but Handlebars needs custom helpers
    // for the operators/formatting/component NgSharp expresses natively. Registered once, compiled once.
    public static readonly string HandlebarsTemplate = Engines.LoadTemplate("Handlebars/showcase.hbs");

    private static readonly HandlebarsDotNet.HandlebarsTemplate<object, object> HbCompiled = BuildHandlebars();

    // Fluid equivalent: Liquid dialect, custom filters delegating to the same helpers (Fmt / InitialsPipe /
    // ImageSrc) so the engines differ only in parse + render. Parser/options static, parsed once (warm).
    public static readonly string FluidTemplate = Engines.LoadTemplate("Fluid/showcase.liquid");

    private static readonly FluidParser LiquidParser = new FluidParser();

    private static readonly Fluid.TemplateOptions FluidOptions = BuildFluidOptions();

    private static readonly IFluidTemplate FluidCompiled = ParseFluid();

    // RazorLight equivalent: full C#, everything native (no helpers) — but cold pays a Roslyn compile.
    public static readonly string RazorTemplate = Engines.LoadTemplate("Razor/showcase.cshtml");

    private static readonly RazorLight.RazorLightEngine Razor = new RazorLight.RazorLightEngineBuilder()
        .UseEmbeddedResourcesProject(typeof(Showcase))
        .UseMemoryCachingProvider()
        .Build();

    // Replicates LargeNumberPipe exactly so the two engines match.
    private static readonly (string Suffix, double Power)[] Magnitudes =
    {
        ("Q", 1e15), ("T", 1e12), ("B", 1e9), ("M", 1e6), ("K", 1e3),
    };

    // Cold: parse + render from scratch each call (first-use latency).
    public static string Cold(Company model)
        => Builder.BuildFromTemplate(Template, model);

    // Warm: template compiled once, model converted (FromObject) + rendered each call.
    public static string Warm(Company model) => Compiled.Render(model);

    // Warm with the NgElement context built once and reused (isolates pure render/interpret cost).
    public static string WarmPrebuilt(NgElement context) => Compiled.Render(context);

    public static NgElement Context(Company model) => NgElement.FromObject(model);

    // Warm: template compiled once, rendered each call.
    public static string Handlebars_Warm(Company model) => HbCompiled(model);

    // Cold: register helpers + compile from scratch each call (Handlebars' codegen cost).
    public static string Handlebars_Cold(Company model) => BuildHandlebars()(model);

    // Warm: template parsed once, render only each call.
    public static string Fluid_Warm(Company model)
        => FluidCompiled.Render(new TemplateContext(model, FluidOptions));

    // Cold: re-parse the template each call, then render (Fluid's per-call parse cost; options stay static,
    // same as the realistic Fluid ports).
    public static string Fluid_Cold(Company model)
    {
        LiquidParser.TryParse(FluidTemplate, out var template, out _);

        return template.Render(new TemplateContext(model, FluidOptions));
    }

    public static string RazorLight_Warm(Company model)
        => Razor.CompileRenderStringAsync("showcase-razor", RazorTemplate, model).GetAwaiter().GetResult();

    public static string RazorLight_Cold(Company model)
        => Razor.CompileRenderStringAsync(Guid.NewGuid().ToString(), RazorTemplate, model).GetAwaiter().GetResult();

    public static Company Model()
    {
        var roles = new[] { "Engineer", "Designer", "Manager", "Analyst", "Lead" };
        var priorities = new[] { "high", "medium", "low" };
        var founded = new DateTime(2009, 6, 1);

        var departments = new List<Department>();

        for (var d = 0; d < 5; d++)
        {
            var teams = new List<Team>();

            for (var t = 0; t < 4; t++)
            {
                var members = new List<Member>();

                for (var m = 0; m < 5; m++)
                {
                    var tasks = new List<TaskItem>();

                    for (var k = 0; k < 4; k++)
                    {
                        tasks.Add(new TaskItem
                        {
                            Title = $"Task {d}-{t}-{m}-{k}",
                            Priority = priorities[k % priorities.Length],
                            Done = (k % 2) == 0,
                            Points = (k * 3) + 1,
                        });
                    }

                    members.Add(new Member
                    {
                        Name = $"Person {d}{t}{m}",
                        Role = roles[m % roles.Length],
                        Salary = 55000m + (m * 7250m),
                        Age = 22 + ((d + t + m) * 3) % 40,
                        IsLead = m == 0,
                        IsRemote = (m % 2) == 1,
                        JoinedAt = founded.AddDays((d * 400) + (t * 90) + (m * 30)),
                        StatusHtml = (m % 3) == 0 ? "<b>active</b>" : "<i>on leave</i>",
                        RemoteLabel = "remote",
                        OnsiteLabel = "on-site",
                        Tasks = tasks,
                    });
                }

                teams.Add(new Team { Name = $"Team {d}-{t}", Members = members });
            }

            departments.Add(new Department
            {
                Name = $"Department {d}",
                Budget = 250000m + (d * 125000m),
                IsCore = (d % 2) == 0,
                ThemeColor = d % 2 == 0 ? "#1E47B5" : "#B4600A",
                Teams = teams,
            });
        }

        // The extended catalogue: every V3 built-in pipe fed data that shows its behavior — a null
        // Nickname (default), owners in capitals (titlecase), a >60-char description (truncate), a
        // status per @switch branch (two 'active', one 'paused', one falling through to @default).
        var projects = new List<Project>
        {
            new Project { Code = 7, Owner = "AMELIE DURAND", Nickname = "phoenix", Status = "active", Budget = 1250000.5m, Tags = new List<string> { "print", "pdf", "rendering" }, Description = "Rebuilds the print pipeline so every quote renders identically across regions and devices." },
            new Project { Code = 42, Owner = "JEAN-PAUL MARTIN", Nickname = null, Status = "paused", Budget = 480000m, Tags = new List<string> { "ops", "tooling" }, Description = "Migrates the legacy exporter to the new engine." },
            new Project { Code = 1043, Owner = "SOFIA BERGSTROM", Nickname = "atlas", Status = "legacy", Budget = 95000.75m, Tags = new List<string> { "archive" }, Description = "Consolidates a decade of archived layouts into one searchable, versioned template catalogue." },
            new Project { Code = 88, Owner = "LI WEI", Nickname = "bamboo", Status = "active", Budget = 2100000m, Tags = new List<string> { "mobile", "print", "qa" }, Description = "Ports the card grid to mobile print heads with automated visual QA on every build." },
        };

        return new Company
        {
            Name = "Contoso",
            LogoHtml = "<svg width=\"48\" height=\"48\"><rect width=\"48\" height=\"48\"/></svg>",
            // A few recognizable bytes (the PNG signature) are enough — the image pipe never decodes.
            Logo = new ImageData { FileName = "logo.png", FileContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            AccentColor = "#1E47B5",
            PrintCss = "article.company > .dept { break-inside: avoid; }",
            Headcount = 5 * 4 * 5,
            FoundedAt = founded,
            ContactEmail = "OPS@CONTOSO.COM",
            Departments = departments,
            Projects = projects,
            Archived = new List<Project>(),
        };
    }

    // Replicates ImagePipe's <img> branch (data URI) for the Handlebars helper and the Razor port.
    public static string ImageSrc(ImageData image)
        => $"data:image/{System.IO.Path.GetExtension(image.FileName).Replace(".", "")};base64,{Convert.ToBase64String(image.FileContent)}";

    // Replicates ImagePipe's non-<img> branch (CSS url(...)).
    public static string ImageUrl(ImageData image) => $"url({ImageSrc(image)})";

    // Replicates CurrencyPipe: the CURRENT culture's 'C' format with the symbol pinned by ISO code
    // (an unknown code becomes its own symbol) — shared by the Handlebars helper, the Fluid filter
    // and the Razor port so all four engines format the same bytes.
    public static string Currency(decimal value, string isoCode)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = isoCode switch
        {
            "EUR" => "€",
            "USD" => "$",
            "GBP" => "£",
            "JPY" => "¥",
            _ => isoCode,
        };

        return value.ToString("C", format);
    }

    // Replicates TruncatePipe: caps at N characters, the U+2026 ellipsis INCLUDED as the last one.
    public static string Truncate(string value, int maxLength)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 1 ? "…" : value.Substring(0, maxLength - 1) + "…";
    }

    // Replicates TitleCasePipe: lowercase first, then TextInfo.ToTitleCase (current culture).
    public static string TitleCase(string value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;

        return textInfo.ToTitleCase(textInfo.ToLower(value));
    }

    // Replicates DefaultPipe: null or blank -> the fallback; anything else passes through.
    public static string DefaultStr(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    // Replicates PadPipe: the value's string form left-padded with '0' to the width.
    public static string Pad(object value, int width)
        => (value?.ToString() ?? string.Empty).PadLeft(width, '0');

    private static HtmlBuilder CreateBuilder()
    {
        var builder = HtmlBuilder.Create();                 // date/number/upper/largeNumber/image pipes
        builder.RegisterComponent<HeadcountBadge>();       // custom component
        builder.RegisterDirective<AuditDirective>();       // custom directive
        builder.RegisterPipe<InitialsPipe>();              // custom pipe

        return builder;
    }

    private static HandlebarsDotNet.HandlebarsTemplate<object, object> BuildHandlebars()
    {
        var hb = HandlebarsDotNet.Handlebars.Create();
        hb.RegisterHelper("gte", (context, args) => ToNum(args[0]) >= ToNum(args[1]));
        hb.RegisterHelper("gt", (context, args) => ToNum(args[0]) > ToNum(args[1]));
        hb.RegisterHelper("eq", (context, args) => Equals(args[0]?.ToString(), args[1]?.ToString()));
        hb.RegisterHelper("and", (context, args) => ToBool(args[0]) && ToBool(args[1]));
        hb.RegisterHelper("or", (context, args) => ToBool(args[0]) || ToBool(args[1]));
        hb.RegisterHelper("upper", (context, args) => args[0]?.ToString()?.ToUpper() ?? string.Empty);
        hb.RegisterHelper("num", (context, args) => Convert.ToDecimal(args[0]).ToString(args[1].ToString()));
        hb.RegisterHelper("date", (context, args) => Convert.ToDateTime(args[0]).ToString(args[1].ToString()));
        hb.RegisterHelper("largeNumber", (context, args) => LargeNumber(Convert.ToDecimal(args[0])));
        hb.RegisterHelper("ne", (context, args) => ToNum(args[0]) != ToNum(args[1]));
        hb.RegisterHelper("len", (context, args) => args[0]?.ToString()?.Length ?? 0);
        hb.RegisterHelper("initials", (context, args) => InitialsPipe.Compute(args[0]?.ToString()));
        hb.RegisterHelper("imgsrc", (context, args) => ImageSrc((ImageData)args[0]));
        hb.RegisterHelper("imgurl", (context, args) => ImageUrl((ImageData)args[0]));

        // The V3 built-in pipes' counterparts (extended catalogue section).
        hb.RegisterHelper("lower", (context, args) => args[0]?.ToString()?.ToLower() ?? string.Empty);
        hb.RegisterHelper("titlecase", (context, args) => TitleCase(args[0]?.ToString()));
        hb.RegisterHelper("pad", (context, args) => Pad(args[0], Convert.ToInt32(args[1])));
        hb.RegisterHelper("default", (context, args) => DefaultStr(args[0]?.ToString(), args[1].ToString()));
        hb.RegisterHelper("truncate", (context, args) => Truncate(args[0]?.ToString(), Convert.ToInt32(args[1])));
        hb.RegisterHelper("join", (context, args) => string.Join(args[1].ToString(), ((System.Collections.IEnumerable)args[0]).Cast<object>()));
        hb.RegisterHelper("currency", (context, args) => Currency(Convert.ToDecimal(args[0]), args[1].ToString()));

        // The ng-template/@render counterpart: a named partial invoked with an explicit context.
        hb.RegisterTemplate("kpi", "<div class=\"kpi\"><b>{{Name}}</b> operates on {{{num Budget \"C0\"}}}</div>");

        return hb.Compile(HandlebarsTemplate);
    }

    // The Liquid counterparts of the pipes: number/date/upper/largeNumber delegate to the shared Fmt
    // replicas (same as the realistic Fluid ports), initials/imgsrc/imgurl to the shared showcase helpers.
    private static Fluid.TemplateOptions BuildFluidOptions()
    {
        var options = new Fluid.TemplateOptions();
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();

        options.Filters.AddFilter("number", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(
                Realistic.Fmt.Number(input.ToNumberValue(), arguments.At(0).ToStringValue()))));

        // Overrides Fluid's built-in date filter so the .NET format string is honoured verbatim (not Ruby strftime).
        options.Filters.AddFilter("date", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(
                Realistic.Fmt.Date(ToDateTime(input), arguments.At(0).ToStringValue()))));

        options.Filters.AddFilter("upper", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Realistic.Fmt.Upper(input.ToStringValue()))));

        options.Filters.AddFilter("largeNumber", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Realistic.Fmt.LargeNumber(input.ToNumberValue()))));

        options.Filters.AddFilter("initials", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(InitialsPipe.Compute(input.ToStringValue() ?? string.Empty))));

        options.Filters.AddFilter("imgsrc", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(ImageSrc((ImageData)input.ToObjectValue()))));

        options.Filters.AddFilter("imgurl", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(ImageUrl((ImageData)input.ToObjectValue()))));

        // The V3 built-in pipes' counterparts (extended catalogue section). Registering an existing
        // Liquid name (default/truncate/join) overrides the built-in filter, aligning its contract on
        // NgSharp's (e.g. Liquid's truncate uses a three-dot suffix, NgSharp a single U+2026).
        options.Filters.AddFilter("lower", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue((input.ToStringValue() ?? string.Empty).ToLower())));

        options.Filters.AddFilter("titlecase", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(TitleCase(input.ToStringValue()))));

        options.Filters.AddFilter("pad", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Pad(input.ToStringValue(), (int)arguments.At(0).ToNumberValue()))));

        options.Filters.AddFilter("default", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(DefaultStr(input.ToStringValue(), arguments.At(0).ToStringValue()))));

        options.Filters.AddFilter("truncate", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Truncate(input.ToStringValue(), (int)arguments.At(0).ToNumberValue()))));

        options.Filters.AddFilter("join", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(string.Join(arguments.At(0).ToStringValue(), input.Enumerate(context).Select(item => item.ToStringValue())))));

        options.Filters.AddFilter("currency", (input, arguments, context) =>
            new ValueTask<FluidValue>(new StringValue(Currency(input.ToNumberValue(), arguments.At(0).ToStringValue() ?? string.Empty))));

        return options;
    }

    private static IFluidTemplate ParseFluid()
    {
        LiquidParser.TryParse(FluidTemplate, out var template, out _);

        return template;
    }

    private static DateTime? ToDateTime(FluidValue input)
    {
        var value = input.ToObjectValue();

        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            _ => null,
        };
    }

    private static double ToNum(object value) => Convert.ToDouble(value);

    private static bool ToBool(object value) => value is bool b ? b : Convert.ToBoolean(value);

    private static string LargeNumber(decimal value)
    {
        var isNegative = value < 0;
        var absolute = (double)Math.Abs(value);

        foreach (var (suffix, power) in Magnitudes)
        {
            var reduced = Math.Round((absolute / power) * 10) / 10;

            if (reduced >= 1)
            {
                return $"{(isNegative ? "-" : string.Empty)}{reduced}{suffix}";
            }
        }

        return value.ToString();
    }
}
