using System.Linq;
using System.Globalization;

namespace NgSharp.Tests.Rendering;

// The TemplateOptions facade: one record carries mode/strict/culture/limits into every Build/Compile/
// Render. Null options (or TemplateOptions.Default) mean the historical defaults; Mode/Strict act at
// compile time, Culture/Limits at render time (memorized by Compile as the template's render defaults,
// overridable per render), Strict acts at both.
public class TemplateOptionsTests
{
    private static readonly CultureInfo French = new CultureInfo("fr-FR");
    private static readonly CultureInfo English = new CultureInfo("en-US");

    #region Null options and Default are the historical defaults

    [Fact]
    public void Null_Options_Render_Byte_Identical_To_Omitting_Them()
    {
        var builder = HtmlBuilder.Create();
        var model = new { Name = "<i>", Ok = true };

        var omitted = builder.BuildFromTemplate("<p>{{ Name }} @if (Ok) {yes}</p>", model);
        var withNull = builder.BuildFromTemplate("<p>{{ Name }} @if (Ok) {yes}</p>", model, null);
        var withDefault = builder.BuildFromTemplate("<p>{{ Name }} @if (Ok) {yes}</p>", model, TemplateOptions.Default);

        Assert.Equal(omitted, withNull);
        Assert.Equal(omitted, withDefault);
        Assert.Contains("&lt;i&gt;", omitted);   // Html stays the default dialect
    }

    [Fact]
    public void Default_Options_Stay_Lenient_And_Uncapped()
    {
        // Missing path renders empty (lenient) and a huge loop is not capped.
        var model = new { Items = Enumerable.Range(0, 100).Select(i => new { i }).ToArray() };

        var html = HtmlBuilder.Create().BuildFromTemplate("<i [for]=\"Items\">{{ Missing }}</i>", model, TemplateOptions.Default);

        Assert.Equal(100, html.Split("<i>").Length - 1);
    }

    #endregion

    #region Each field acts

    [Fact]
    public void Mode_Picks_The_Text_Dialect()
        => Assert.Equal("a & <b>",
            HtmlBuilder.Create().BuildFromTemplate("{{ X }}", new { X = "a & <b>" }, new TemplateOptions { Mode = TemplateMode.Text }));

    [Fact]
    public void Strict_Gates_The_Compile_Through_Validation()
    {
        var exception = Assert.Throws<NgSharpException>(
            () => HtmlBuilder.Create().Compile("@for (x in Items) {<li>{{ x }}</li>}", new TemplateOptions { Strict = true }));

        Assert.Contains("validation error", exception.Message);
    }

    [Fact]
    public void Strict_Makes_A_Missing_Path_Throw_At_Render()
    {
        var exception = Assert.Throws<NgSharpException>(
            () => HtmlBuilder.Create().BuildFromTemplate("<p>{{ Missing }}</p>", new { Name = "x" }, new TemplateOptions { Strict = true }));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public void Culture_Formats_The_Render_And_Is_Restored_Afterwards()
    {
        var html = HtmlBuilder.Create().BuildFromTemplate(
            "<p>{{ Price | number:'N2' }}</p>", new { Price = 3.5 }, new TemplateOptions { Culture = French });

        Assert.Equal("<p>3,50</p>", html);
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);   // TestCulture pins invariant
    }

    [Fact]
    public void Limits_Cap_The_Render()
    {
        var model = new { Items = Enumerable.Range(0, 100).Select(i => new { i }).ToArray() };

        var exception = Assert.Throws<NgSharpException>(
            () => HtmlBuilder.Create().BuildFromTemplate(
                "<i [for]=\"Items\">x</i>", model, new TemplateOptions { Limits = new RenderLimits(maxLoopIterations: 10) }));

        Assert.Contains("Render limit exceeded", exception.Message);
    }

    #endregion

    #region Strict precedence: options > builder > lenient; render options > compiled default

    [Fact]
    public void Options_Strict_False_Overrides_A_Strict_Builder()
    {
        var html = HtmlBuilder.Create(strict: true).BuildFromTemplate(
            "<p>{{ Missing }}</p>", new { Name = "x" }, new TemplateOptions { Strict = false });

        Assert.Contains("<p></p>", html);
    }

    [Fact]
    public void Render_Options_Strict_Overrides_The_Compiled_Default_Both_Ways()
    {
        var lenient = HtmlBuilder.Create().Compile("<p>{{ Missing }}</p>");
        var strict = HtmlBuilder.Create().Compile("<p>{{ Missing }}</p>", new TemplateOptions { Strict = true });

        // Compiled lenient, rendered strict: throws.
        Assert.Throws<NgSharpException>(() => lenient.Render(new { Name = "x" }, new TemplateOptions { Strict = true }));

        // Compiled strict, rendered with an explicit false: lenient again.
        Assert.Contains("<p></p>", strict.Render(new { Name = "x" }, new TemplateOptions { Strict = false }));

        // Null render options keep each compiled default.
        Assert.Contains("<p></p>", lenient.Render(new { Name = "x" }));
        Assert.Throws<NgSharpException>(() => strict.Render(new { Name = "x" }));
    }

    #endregion

    #region Compile memorizes Culture/Limits as render defaults; render options override them

    [Fact]
    public void Culture_Given_At_Compile_Is_The_Render_Default_And_A_Render_Culture_Wins()
    {
        var compiled = HtmlBuilder.Create().Compile(
            "<p>{{ Price | number:'N2' }}</p>", new TemplateOptions { Culture = French });

        Assert.Equal("<p>3,50</p>", compiled.Render(new { Price = 3.5 }));
        Assert.Equal("<p>3.50</p>", compiled.Render(new { Price = 3.5 }, new TemplateOptions { Culture = English }));
        Assert.Equal("<p>3,50</p>", compiled.Render(new { Price = 3.5 }));   // the compiled default survives the override
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void Limits_Given_At_Compile_Are_The_Render_Default_And_Render_Limits_Win()
    {
        var model = new { Items = Enumerable.Range(0, 100).Select(i => new { i }).ToArray() };
        var compiled = HtmlBuilder.Create().Compile(
            "<i [for]=\"Items\">x</i>", new TemplateOptions { Limits = new RenderLimits(maxLoopIterations: 10) });

        Assert.Throws<NgSharpException>(() => compiled.Render(model));
        Assert.Equal(100, compiled.Render(model, new TemplateOptions { Limits = RenderLimits.None }).Split("<i>").Length - 1);
    }

    [Fact]
    public void Mode_In_Render_Options_Contradicting_The_Compiled_Dialect_Throws()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>");

        var exception = Assert.Throws<NgSharpException>(
            () => compiled.Render(new { Name = "<b>" }, new TemplateOptions { Mode = TemplateMode.Text }));

        Assert.Same(TemplateMode.Html, compiled.Mode);
        Assert.Contains("compiled in Html mode", exception.Message);
    }

    [Fact]
    public void Mode_In_Render_Options_Matching_The_Compiled_Dialect_Is_Fine()
    {
        var options = new TemplateOptions { Mode = TemplateMode.Text };
        var compiled = HtmlBuilder.Create().Compile("{{ Name }}", options);

        // One options instance serves Compile AND Render — only a CONTRADICTING mode throws.
        Assert.Equal("<b>", compiled.Render(new { Name = "<b>" }, options));
    }

    #endregion

    #region The record is a value: equality and with-derivation

    [Fact]
    public void Options_Are_Compared_By_Value()
    {
        var a = new TemplateOptions { Mode = TemplateMode.Text, Strict = true, Culture = French };
        var b = new TemplateOptions { Mode = TemplateMode.Text, Strict = true, Culture = French };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { Strict = false });
    }

    [Fact]
    public void With_Derives_A_Variant_Without_Touching_The_Original()
    {
        var options = new TemplateOptions { Culture = French, Strict = true };

        var english = options with { Culture = English };

        Assert.Same(French, options.Culture);
        Assert.Same(English, english.Culture);
        Assert.True(english.Strict);
    }

    #endregion
}
