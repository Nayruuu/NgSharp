using System.Globalization;

using NgSharp;

namespace NgSharp.Tests.Rendering;

// The per-render culture overloads: CurrentCulture/CurrentUICulture are swapped around the render and
// restored in a finally, so one process (or one CompiledTemplate) serves multiple locales without
// touching thread state. The test run itself is pinned to InvariantCulture (TestCulture), which is
// exactly what must be observed again after every overload returns.
public class CultureRenderTests
{
    private static readonly CultureInfo French = new CultureInfo("fr-FR");
    private static readonly CultureInfo English = new CultureInfo("en-US");

    [Fact]
    public void Sync_Build_Renders_Under_The_Given_Culture()
        => Assert.Equal("<p>3,50</p>",
            HtmlBuilder.Create().BuildFromTemplate("<p>{{ Price | number:'N2' }}</p>", new { Price = 3.5 }, new TemplateOptions { Culture = French }));

    [Fact]
    public void The_Ambient_Culture_Is_Restored_After_The_Render()
    {
        HtmlBuilder.Create().BuildFromTemplate("<p>{{ Price | number:'N2' }}</p>", new { Price = 3.5 }, new TemplateOptions { Culture = French });

        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void The_Ambient_Culture_Is_Restored_Even_When_The_Render_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            HtmlBuilder.Create().BuildFromTemplate("<p>{{ X | unknownPipe }}</p>", new { X = 1 }, new TemplateOptions { Culture = French }));

        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void Null_Culture_Keeps_The_Ambient_Culture()
        => Assert.Equal("<p>3.50</p>",
            HtmlBuilder.Create().BuildFromTemplate("<p>{{ Price | number:'N2' }}</p>", new { Price = 3.5 }, new TemplateOptions { Culture = null }));

    [Fact]
    public void One_Compiled_Template_Serves_Two_Locales()
    {
        var template = HtmlBuilder.Create().Compile("<p>{{ Price | number:'N2' }}</p>");

        Assert.Equal("<p>3,50</p>", template.Render(new { Price = 3.5 }, new TemplateOptions { Culture = French }));
        Assert.Equal("<p>3.50</p>", template.Render(new { Price = 3.5 }, new TemplateOptions { Culture = English }));
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void Text_Mode_Bare_Numbers_Stay_Invariant_Whatever_The_Culture()
        => Assert.Equal("3.5;3,50",
            HtmlBuilder.Create().BuildFromTemplate(
                "{{ Price }};{{ Price | number:'N2' }}", new { Price = 3.5 }, new TemplateOptions { Culture = French, Mode = TemplateMode.Text }));
}
