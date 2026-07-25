using System.Text.Json;

using NgSharp;

namespace NgSharp.Tests.Rendering;

// BuildFromTemplate — the three synchronous entry points (object, JsonElement, NgElement; rendering
// is CPU-bound, there is no async overload) — the empty-template guard, and the Create() factory.
public class SyncBuildTests
{
    [Fact]
    public void Object_Overload_Renders_Through_Reflection()
        => Assert.Equal("<p>ADA</p>",
            HtmlBuilder.Create().BuildFromTemplate("<p>{{ Name | upper }}</p>", new { Name = "Ada" }));

    [Fact]
    public void JsonElement_Overload_Renders_The_Reflection_Free_Path()
    {
        using var json = JsonDocument.Parse("{\"Name\":\"Ada\"}");

        Assert.Equal("<p>Ada</p>",
            HtmlBuilder.Create().BuildFromTemplate("<p>{{ Name }}</p>", json.RootElement));
    }

    [Fact]
    public void NgElement_Overload_Renders_The_Prebuilt_Context()
        => Assert.Equal("<p>Ada</p>",
            HtmlBuilder.Create().BuildFromTemplate("<p>{{ Name }}</p>", NgElement.FromObject(new { Name = "Ada" })));

    [Fact]
    public void Overloads_Take_The_Template_Mode()
        => Assert.Equal("Hello Ada",
            HtmlBuilder.Create().BuildFromTemplate("Hello {{ Name }}", new { Name = "Ada" }, new TemplateOptions { Mode = TemplateMode.Text }));

    [Fact]
    public void Empty_Template_Throws_The_Guard()
        => Assert.Throws<NgSharpException>(() => HtmlBuilder.Create().BuildFromTemplate("", new { X = 1 }));

    [Fact]
    public void Create_Returns_A_Fresh_Builder_Every_Call()
        => Assert.NotSame(HtmlBuilder.Create(), HtmlBuilder.Create());
}
