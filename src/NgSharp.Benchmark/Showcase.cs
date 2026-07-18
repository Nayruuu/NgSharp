using System;
using System.Collections.Generic;

using HandlebarsDotNet;

using NgSharp;
using NgSharp.Components;

namespace NgSharp.Benchmark
{
    // A deep, feature-complete NgSharp-only benchmark: one document that exercises everything the engine
    // does — 4 levels of nested [for], @if/@else-if/@else + [if] with &&/||/comparisons + ternary,
    // [not-empty], [class.x]/[attr.x]/[style.x]/[html] bindings, the date/number/upper/largeNumber pipes,
    // .Count members, and a custom component. No cross-engine parity here (Mustache/Handlebars can't
    // express most of this), so it measures NgSharp on a realistic complex template rather than the
    // lowest-common-denominator document the 6-engine comparison is limited to.
    public static class Showcase
    {
        public static readonly string Template = Engines.LoadTemplate("showcase.html");

        private static readonly HtmlBuilder Builder = CreateBuilder();

        private static readonly CompiledTemplate Compiled = Builder.Compile(Template);

        private static HtmlBuilder CreateBuilder()
        {
            var builder = HtmlBuilder.Default;                 // date/number/upper/largeNumber pipes
            builder.RegisterComponent<HeadcountBadge>();       // custom component
            return builder;
        }

        // Cold: parse + render from scratch each call (first-use latency).
        public static string Cold(Company model)
            => Builder.BuildFromTemplateAsync(Template, model).GetAwaiter().GetResult();

        // Warm: template compiled once, model converted (FromObject) + rendered each call.
        public static string Warm(Company model) => Compiled.Render(model);

        // Warm with the NgElement context built once and reused (isolates pure render/interpret cost).
        public static string WarmPrebuilt(NgElement context) => Compiled.Render(context);

        public static NgElement Context(Company model) => NgElement.FromObject(model);

        // ---- Handlebars.Net equivalent -------------------------------------
        // Same document, byte-identical output — but Handlebars needs custom helpers for the operators,
        // formatting and component that NgSharp expresses natively. Registered once, compiled once.
        public static readonly string HandlebarsTemplate = Engines.LoadTemplate("showcase.handlebars");

        private static readonly HandlebarsDotNet.HandlebarsTemplate<object, object> HbCompiled = BuildHandlebars();

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
            return hb.Compile(HandlebarsTemplate);
        }

        // Warm: template compiled once, rendered each call.
        public static string Handlebars_Warm(Company model) => HbCompiled(model);

        // Cold: register helpers + compile from scratch each call (Handlebars' codegen cost).
        public static string Handlebars_Cold(Company model) => BuildHandlebars()(model);

        // ---- RazorLight equivalent -----------------------------------------
        // Razor is full C#, so it expresses everything natively (no helpers) — but it compiles the
        // template to an assembly via Roslyn, so its cold start is heavy.
        public static readonly string RazorTemplate = Engines.LoadTemplate("showcase.razorlight.cshtml");

        private static readonly RazorLight.RazorLightEngine Razor = new RazorLight.RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(Showcase))
            .UseMemoryCachingProvider()
            .Build();

        public static string RazorLight_Warm(Company model)
            => Razor.CompileRenderStringAsync("showcase-razor", RazorTemplate, model).GetAwaiter().GetResult();

        public static string RazorLight_Cold(Company model)
            => Razor.CompileRenderStringAsync(Guid.NewGuid().ToString(), RazorTemplate, model).GetAwaiter().GetResult();

        private static double ToNum(object value) => Convert.ToDouble(value);

        private static bool ToBool(object value) => value is bool b ? b : Convert.ToBoolean(value);

        // Replicates LargeNumberPipe exactly so the two engines match.
        private static readonly (string Suffix, double Power)[] Magnitudes =
        {
            ("Q", 1e15), ("T", 1e12), ("B", 1e9), ("M", 1e6), ("K", 1e3),
        };

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

            return new Company
            {
                Name = "Contoso",
                LogoHtml = "<svg width=\"48\" height=\"48\"><rect width=\"48\" height=\"48\"/></svg>",
                Headcount = 5 * 4 * 5,
                FoundedAt = founded,
                Departments = departments,
            };
        }
    }

    // A custom component (server-rendered HTML fragment). Property set from [total]="..." on the tag.
    public sealed class HeadcountBadge : IComponent
    {
        public string ComponentName => "headcount-badge";

        public int Total { get; set; }

        public string Render() => $"<footer class=\"headcount\">Total headcount: {Total}</footer>";
    }

    public sealed class Company
    {
        public string Name { get; set; }
        public string LogoHtml { get; set; }
        public int Headcount { get; set; }
        public DateTime FoundedAt { get; set; }
        public List<Department> Departments { get; set; }
    }

    public sealed class Department
    {
        public string Name { get; set; }
        public decimal Budget { get; set; }
        public bool IsCore { get; set; }
        public string ThemeColor { get; set; }
        public List<Team> Teams { get; set; }
    }

    public sealed class Team
    {
        public string Name { get; set; }
        public List<Member> Members { get; set; }
    }

    public sealed class Member
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public decimal Salary { get; set; }
        public int Age { get; set; }
        public bool IsLead { get; set; }
        public bool IsRemote { get; set; }
        public DateTime JoinedAt { get; set; }
        public string StatusHtml { get; set; }
        public string RemoteLabel { get; set; }
        public string OnsiteLabel { get; set; }
        public List<TaskItem> Tasks { get; set; }
    }

    public sealed class TaskItem
    {
        public string Title { get; set; }
        public string Priority { get; set; }
        public bool Done { get; set; }
        public int Points { get; set; }
    }
}
