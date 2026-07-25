using System;
using System.Globalization;

using NgSharp;
using NgSharp.Tests.CustomElements;

namespace NgSharp.Tests.Rendering;

// Interpolation ({{ }}) and pipe edge cases: whitespace tolerance, adjacency, chaining, multiple
// different pipes in one text node, value types, nested paths.
public class InterpolationAndPipeTests
{
    [Fact]
    public void Interpolation_Without_Spaces()
        => Assert.Contains("<p>alice</p>", Render("<p>{{Name}}</p>", new { Name = "alice" }));

    [Fact]
    public void Interpolation_With_Extra_Spaces()
        => Assert.Contains("<p>alice</p>", Render("<p>{{    Name    }}</p>", new { Name = "alice" }));

    [Fact]
    public void Adjacent_Interpolations_No_Separator()
        => Assert.Contains("<p>ab</p>", Render("<p>{{ A }}{{ B }}</p>", new { A = "a", B = "b" }));

    [Fact]
    public void Interpolation_Keeps_Surrounding_Text()
        => Assert.Contains("<p>Hi alice, welcome</p>", Render("<p>Hi {{ Name }}, welcome</p>", new { Name = "alice" }));

    [Fact]
    public void Interpolation_Number()
        => Assert.Contains("<p>42</p>", Render("<p>{{ N }}</p>", new { N = 42 }));

    [Fact]
    public void Interpolation_Bool()
        => Assert.Contains("<p>True</p>", Render("<p>{{ B }}</p>", new { B = true }));

    [Fact]
    public void Interpolation_Null_Renders_Empty()
        => Assert.Contains("<p></p>", Render("<p>{{ X }}</p>", new { X = (string)null }));

    [Fact]
    public void Interpolation_Nested_Path()
        => Assert.Contains("<p>Paris</p>", Render("<p>{{ User.City }}</p>", new { User = new { City = "Paris" } }));

    [Fact]
    public void Pipe_With_Quoted_Argument()
        => Assert.Contains("<p>13/03/2021</p>", Render("<p>{{ D | date: 'dd/MM/yyyy' }}</p>", new { D = new DateTime(2021, 3, 13) }));

    [Fact]
    public void Pipe_On_Nested_Path()
        => Assert.Contains("<p>PARIS</p>", Render("<p>{{ User.City | upper }}</p>", new { User = new { City = "paris" } }));

    [Fact]
    public void Chained_Pipes()
        => Assert.Contains("<p>alice</p>", Render("<p>{{ Name | upper | lower }}</p>", new { Name = "AlIcE" }));

    [Fact]
    public void Chained_Pipe_With_Argument_Then_Transform()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        // number:'N0' -> "1,234", then lower (no letters) -> "1,234"; exercises chaining a pipe-with-arg.
        var content = Render("<p>{{ N | number: 'N0' | lower }}</p>", new { N = 1234 });

        Assert.Contains("<p>1,234</p>", content);
    }

    [Fact]
    public void Two_Different_Pipes_In_One_Text_Node()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var content = Render("<p>{{ Name | upper }} scored {{ Score | number: 'N0' }}</p>",
            new { Name = "alice", Score = 1234 });

        Assert.Contains("<p>ALICE scored 1,234</p>", content);
    }

    [Fact]
    public void Unknown_Pipe_Throws_With_Its_Name()
    {
        var ex = Assert.ThrowsAny<Exception>(() => Render("<p>{{ X | nope }}</p>", new { X = "x" }));

        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void Reregistered_Builtin_Pipe_Name_Uses_The_Custom_Implementation()
    {
        // The interpolation span fast path keys on the RESOLVED INSTANCE (ISpanPipe), never the name:
        // a custom pipe re-registered under "number" must win over the built-in formatting fast path.
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe<HijackingNumberPipe>();

        var content = builder.BuildFromTemplate("<p>{{ Score | number: 'N0' }}</p>", new { Score = 1234 });

        Assert.Contains("<p>hijacked</p>", content);
    }

    [Fact]
    public void Stateful_Source_Pipe_Runs_Once_When_The_Span_Fast_Path_Declines()
    {
        // {{ X | seq | date }}: 'date' resolves as a span pipe but its no-argument form declines
        // TryTransform AFTER the source ({{ X | seq }}) was evaluated — the render must then finish
        // in place, never fall back to a full re-evaluation (which would run 'seq' twice per render).
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe<CountingSeqPipe>();
        CountingSeqPipe.Calls = 0;

        var content = builder.BuildFromTemplate("<p>{{ X | seq | date }}</p>", new { X = "x" });

        Assert.Equal(1, CountingSeqPipe.Calls);
        Assert.Contains("<p></p>", content);
    }

    private static string Render(string tpl, object model)
    {
        var builder = HtmlBuilder.Create();   // has date/upper/number/largeNumber/image built-in
        builder.RegisterPipe<LowerCasePipe>();

        return builder.BuildFromTemplate(tpl, model);
    }

    private sealed class HijackingNumberPipe : NgSharp.Pipes.IPipe
    {
        public string PipeName => "number";

        public string Transform(string tagName, NgElement value, string argument) => "hijacked";
    }

    private sealed class CountingSeqPipe : NgSharp.Pipes.IPipe
    {
        public static int Calls;

        public string PipeName => "seq";

        // Returns a non-date string so the trailing 'date' pipe renders empty, deterministically.
        public string Transform(string tagName, NgElement value, string argument)
        {
            Calls++;

            return "not-a-date";
        }
    }
}
