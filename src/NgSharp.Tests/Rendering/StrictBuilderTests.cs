namespace NgSharp.Tests.Rendering;

// HtmlBuilder.Create(strict: true): the builder-level strict default applies to EVERY Build/Compile
// without repeating the flag — and a per-call strict argument always overrides it (call > builder).
public class StrictBuilderTests
{
    #region The builder default applies everywhere

    [Fact]
    public void Strict_Builder_Applies_Strict_To_A_Plain_BuildFromTemplate()
    {
        var builder = HtmlBuilder.Create(strict: true);

        var exception = Assert.Throws<NgSharpException>(
            () => builder.BuildFromTemplate("<p>{{ Missing }}</p>", new { Name = "x" }));

        Assert.Contains("Strict mode", exception.Message);
        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public void Strict_Builder_Applies_Strict_To_A_Plain_Compile()
    {
        var compiled = HtmlBuilder.Create(strict: true).Compile("<p>{{ Name }}</p>");

        Assert.True(compiled.Strict);
        Assert.Throws<NgSharpException>(() => compiled.Render(new { Other = 1 }));
    }

    [Fact]
    public void Strict_Builder_Runs_The_Validation_Gate_On_A_One_Shot_Render()
    {
        var builder = HtmlBuilder.Create(strict: true);

        var exception = Assert.Throws<NgSharpException>(
            () => builder.BuildFromTemplate("@for (x in Items) {<li>{{ x }}</li>}", new { Items = new[] { 1 } }));

        Assert.Contains("validation error", exception.Message);
    }

    #endregion

    #region A per-call strict argument overrides the builder (call > builder)

    [Fact]
    public void Explicit_False_On_The_Call_Overrides_A_Strict_Builder()
    {
        var builder = HtmlBuilder.Create(strict: true);

        var html = builder.BuildFromTemplate("<p>{{ Missing }}</p>", new { Name = "x" }, new TemplateOptions { Strict = false });

        Assert.Contains("<p></p>", html);
    }

    [Fact]
    public void Explicit_False_On_Compile_Overrides_A_Strict_Builder()
    {
        var compiled = HtmlBuilder.Create(strict: true).Compile("<p>{{ Missing }}</p>", new TemplateOptions { Strict = false });

        Assert.False(compiled.Strict);
        Assert.Contains("<p></p>", compiled.Render(new { Name = "x" }));
    }

    [Fact]
    public void Explicit_True_On_The_Call_Overrides_A_Lenient_Builder()
    {
        var builder = HtmlBuilder.Create(strict: false);

        var exception = Assert.Throws<NgSharpException>(
            () => builder.BuildFromTemplate("<p>{{ Missing }}</p>", new { Name = "x" }, new TemplateOptions { Strict = true }));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public void A_Default_Builder_Stays_Lenient_Everywhere()
    {
        var builder = HtmlBuilder.Create();

        Assert.Contains("<p></p>", builder.BuildFromTemplate("<p>{{ Missing }}</p>", new { Name = "x" }));
        Assert.False(builder.Compile("<p>{{ Missing }}</p>").Strict);
    }

    #endregion
}
