using System.Linq;

namespace NgSharp.Tests.Rendering;

// Opt-in resource caps (RenderLimits) for untrusted templates: each cap throws with a clear message
// when exceeded, and the default path (null / RenderLimits.None / unreached caps) stays byte-identical.
public class RenderLimitsTests
{
    [Fact]
    public void MaxOutputChars_Exceeded_Throws_With_A_Clear_Message()
    {
        var template = HtmlBuilder.Create().Compile("<i [for]=\"Items\">aaaaaaaaaa</i>");
        var model = new { Items = Enumerable.Range(0, 1_000).Select(i => new { i }).ToArray() };

        var exception = Assert.Throws<NgSharpException>(() => template.Render(model, new TemplateOptions { Limits = new RenderLimits(maxOutputChars: 1_000) }));

        Assert.Contains("Render limit exceeded", exception.Message);
        Assert.Contains("MaxOutputChars = 1000", exception.Message);
    }

    [Fact]
    public void MaxLoopIterations_Exceeded_Throws_With_A_Clear_Message()
    {
        var template = HtmlBuilder.Create().Compile("<i [for]=\"Items\">{{ Name }}</i>");
        var model = new { Items = Enumerable.Range(0, 100).Select(i => new { Name = "x" }).ToArray() };

        var exception = Assert.Throws<NgSharpException>(() => template.Render(model, new TemplateOptions { Limits = new RenderLimits(maxLoopIterations: 10) }));

        Assert.Contains("Render limit exceeded", exception.Message);
        Assert.Contains("MaxLoopIterations = 10", exception.Message);
    }

    [Fact]
    public void MaxRenderDepth_Exceeded_By_A_Recursive_Fragment_Throws_With_A_Clear_Message()
    {
        var template = HtmlBuilder.Create().Compile("<ng-template #r>x@render(r)</ng-template>@render(r)");

        var exception = Assert.Throws<NgSharpException>(() => template.Render(new { }, new TemplateOptions { Limits = new RenderLimits(maxRenderDepth: 10) }));

        Assert.Contains("Render limit exceeded", exception.Message);
        Assert.Contains("MaxRenderDepth = 10", exception.Message);
    }

    [Fact]
    public void Without_Limits_A_Recursive_Fragment_Still_Stops_Silently_At_The_BuiltIn_Depth()
    {
        // The pre-limits contract, unchanged: depth 50, no exception, truncated output.
        var template = HtmlBuilder.Create().Compile("<ng-template #r>x@render(r)</ng-template>@render(r)");

        var output = template.Render(new { });

        Assert.Equal(new string('x', 50), output);
    }

    [Fact]
    public void None_Renders_Byte_Identical_To_The_Default()
    {
        var template = HtmlBuilder.Create().Compile("<ul><li [for]=\"Items\" [class.hot]=\"Hot\">{{ Name | upper }} — {{ Price }}</li></ul>");
        var model = new { Items = new[] { new { Name = "ada", Price = 12.5, Hot = true }, new { Name = "linus", Price = 3.0, Hot = false } } };

        var unlimited = template.Render(model);
        var none = template.Render(model, new TemplateOptions { Limits = RenderLimits.None });

        Assert.Equal(unlimited, none);
    }

    [Fact]
    public void Unreached_Limits_Render_Byte_Identical_To_The_Default()
    {
        var template = HtmlBuilder.Create().Compile("<ng-template #card><b>{{ Name }}</b></ng-template><div [for]=\"Items\">@render(card, Items[0]){{ Name | upper }}</div>");
        var model = new { Items = new[] { new { Name = "ada" }, new { Name = "linus" } } };

        var unlimited = template.Render(model);
        var limited = template.Render(model, new TemplateOptions { Limits = new RenderLimits() });

        Assert.Equal(unlimited, limited);
    }

    [Fact]
    public void Limits_Apply_On_The_Builder_Build_Path_Too()
    {
        var model = new { Items = Enumerable.Range(0, 100).Select(i => new { Name = "x" }).ToArray() };

        var exception = Assert.Throws<NgSharpException>(
            () => HtmlBuilder.Create().BuildFromTemplate("<i [for]=\"Items\">{{ Name }}</i>", model, new TemplateOptions { Limits = new RenderLimits(maxLoopIterations: 10) }));

        Assert.Contains("Render limit exceeded", exception.Message);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void NonPositive_Caps_Are_Rejected_At_Construction(int maxOutputChars, int maxLoopIterations, int maxRenderDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderLimits(maxOutputChars, maxLoopIterations, maxRenderDepth));
    }
}
