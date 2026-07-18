using System;
using System.Globalization;
using System.Threading.Tasks;

using NgSharp;
using NgSharp.Tests.CustomElements;

namespace NgSharp.Tests;

// Interpolation ({{ }}) and pipe edge cases: whitespace tolerance, adjacency, chaining, multiple
// DIFFERENT pipes in one text node (a known 1.0.x gap), value types, nested paths.
public class InterpolationAndPipeTests
{
    private static Task<string> Render(string tpl, object model)
    {
        var builder = HtmlBuilder.Default;   // has date/upper/number/largeNumber/image built-in
        builder.RegisterPipe<LowerCasePipe>();
        return builder.BuildFromTemplateAsync(tpl, model);
    }

    // ---- {{ }} whitespace / adjacency ----

    [Fact]
    public async Task Interpolation_Without_Spaces()
        => Assert.Contains("<p>alice</p>", await Render("<p>{{Name}}</p>", new { Name = "alice" }));

    [Fact]
    public async Task Interpolation_With_Extra_Spaces()
        => Assert.Contains("<p>alice</p>", await Render("<p>{{    Name    }}</p>", new { Name = "alice" }));

    [Fact]
    public async Task Adjacent_Interpolations_No_Separator()
        => Assert.Contains("<p>ab</p>", await Render("<p>{{ A }}{{ B }}</p>", new { A = "a", B = "b" }));

    [Fact]
    public async Task Interpolation_Keeps_Surrounding_Text()
        => Assert.Contains("<p>Hi alice, welcome</p>", await Render("<p>Hi {{ Name }}, welcome</p>", new { Name = "alice" }));

    // ---- value kinds ----

    [Fact]
    public async Task Interpolation_Number()
        => Assert.Contains("<p>42</p>", await Render("<p>{{ N }}</p>", new { N = 42 }));

    [Fact]
    public async Task Interpolation_Bool()
        => Assert.Contains("<p>True</p>", await Render("<p>{{ B }}</p>", new { B = true }));

    [Fact]
    public async Task Interpolation_Null_Renders_Empty()
        => Assert.Contains("<p></p>", await Render("<p>{{ X }}</p>", new { X = (string)null }));

    [Fact]
    public async Task Interpolation_Nested_Path()
        => Assert.Contains("<p>Paris</p>", await Render("<p>{{ User.City }}</p>", new { User = new { City = "Paris" } }));

    // ---- pipes ----

    [Fact]
    public async Task Pipe_With_Quoted_Argument()
        => Assert.Contains("<p>13/03/2021</p>", await Render("<p>{{ D | date: 'dd/MM/yyyy' }}</p>", new { D = new DateTime(2021, 3, 13) }));

    [Fact]
    public async Task Pipe_On_Nested_Path()
        => Assert.Contains("<p>PARIS</p>", await Render("<p>{{ User.City | upper }}</p>", new { User = new { City = "paris" } }));

    [Fact]
    public async Task Chained_Pipes()
        => Assert.Contains("<p>alice</p>", await Render("<p>{{ Name | upper | lower }}</p>", new { Name = "AlIcE" }));

    [Fact]
    public async Task Chained_Pipe_With_Argument_Then_Transform()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        // number:'N0' -> "1,234", then lower (no letters) -> "1,234"; exercises chaining a pipe-with-arg.
        var content = await Render("<p>{{ N | number: 'N0' | lower }}</p>", new { N = 1234 });

        Assert.Contains("<p>1,234</p>", content);
    }

    [Fact]
    public async Task Two_Different_Pipes_In_One_Text_Node()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        // The 1.0.x engine could not apply two DIFFERENT pipes in one text node; v2 parses each {{ }}
        // independently, so it must.
        var content = await Render("<p>{{ Name | upper }} scored {{ Score | number: 'N0' }}</p>",
            new { Name = "alice", Score = 1234 });

        Assert.Contains("<p>ALICE scored 1,234</p>", content);
    }

    [Fact]
    public async Task Unknown_Pipe_Throws_With_Its_Name()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Render("<p>{{ X | nope }}</p>", new { X = "x" }));

        Assert.Contains("nope", ex.Message);
    }
}
