
namespace NgSharp.Tests.Rendering;

// {{- / -}} whitespace control (Scriban/Liquid-style), resolved at PARSE time: '{{-' trims the
// whitespace (newlines included) that precedes it in the surrounding text run, '-}}' the whitespace
// that follows. A marker is only active glued to its braces WITH a whitespace on the expression side
// — '{{ -X }}' and '{{-X }}' stay negations, and templates without markers are byte-identical.
public class WhitespaceControlTests
{
    #region Trim left / right / both

    [Fact]
    public void Left_Marker_Trims_The_Whitespace_Before_The_Interpolation()
        => Assert.Equal("Hello:Bob!", Text("Hello:   {{- Name }}!", new { Name = "Bob" }));

    [Fact]
    public void Right_Marker_Trims_The_Whitespace_After_The_Interpolation()
        => Assert.Equal("Bob!", Text("{{ Name -}}   !", new { Name = "Bob" }));

    [Fact]
    public void Both_Markers_Trim_Both_Sides()
        => Assert.Equal("a1b", Text("a  {{- X -}}  b", new { X = 1 }));

    [Fact]
    public void Markers_Eat_Newlines_Across_Lines()
        // The plain-text email case: the line breaks around the interpolation disappear.
        => Assert.Equal("Line1Bob", Text("Line1\n\n{{- Name -}}\n\n", new { Name = "Bob" }));

    #endregion

    #region The email @if pattern ({{- -}} whitespace eater)

    [Fact]
    public void Whitespace_Eater_Renders_Nothing_And_Trims_Both_Sides()
        => Assert.Equal("ab", Text("a \n {{- -}} \n b", new { }));

    [Fact]
    public void Conditional_Line_In_An_Email_Vanishes_Cleanly_With_Eaters()
    {
        // The canonical remedy for the '@if line': eaters swallow the block's own line breaks, so the
        // taken branch keeps single line spacing and the skipped branch leaves no blank line at all.
        const string template = "Bonjour {{ Name }},\n@if (Vip) {\n{{- -}}\nMerci pour votre fidélité !\n}{{- -}}\nCordialement";

        var vip = Text(template, new { Name = "Alice", Vip = true });
        var standard = Text(template, new { Name = "Alice", Vip = false });

        Assert.Equal("Bonjour Alice,\nMerci pour votre fidélité !\nCordialement", vip);
        Assert.Equal("Bonjour Alice,\nCordialement", standard);
    }

    #endregion

    #region Negation and subtraction are never captured

    [Fact]
    public void Dash_Not_Glued_To_The_Braces_Is_A_Negation()
        => Assert.Equal("a -5 b", Text("a {{ -X }} b", new { X = 5 }));

    [Fact]
    public void Glued_Dash_Without_A_Following_Whitespace_Is_A_Negation()
        => Assert.Equal("a -5 b", Text("a {{-X }} b", new { X = 5 }));

    [Fact]
    public void Subtraction_Still_Works_Next_To_A_Right_Marker()
        => Assert.Equal("a4", Text("a {{- X - 1 -}} ", new { X = 5 }));

    #endregion

    #region No marker = byte-identical; boundaries

    [Fact]
    public void Without_Markers_Whitespace_Is_Untouched_In_Both_Modes()
    {
        Assert.Equal("  Bob  ", Text("  {{ Name }}  ", new { Name = "Bob" }));
        Assert.Equal("<p>  Bob  </p>", Html("<p>  {{ Name }}  </p>", new { Name = "Bob" }));
    }

    [Fact]
    public void Markers_Trim_Template_Whitespace_Never_Data_Whitespace()
        // The trims apply to the literal text around the braces — a value's own spaces survive.
        => Assert.Equal("a  b", Text("{{ A -}} {{- B }}", new { A = "a ", B = " b" }));

    #endregion

    #region Both modes, with pipe

    [Fact]
    public void Markers_Work_In_Html_Mode_And_Escaping_Still_Applies()
    {
        Assert.Equal("<p>A &amp; B  </p>", Html("<p>  {{- Name }}  </p>", new { Name = "A & B" }));
        Assert.Equal("<p>Bob!</p>", Html("<p>{{ Name -}}   !</p>", new { Name = "Bob" }));
    }

    [Fact]
    public void Markers_Work_With_Pipes()
        => Assert.Equal("BOB!", Text("{{ Name | upper -}}   !", new { Name = "Bob" }));

    #endregion

    [Fact]
    public void Whitespace_Eater_Passes_Validation_While_Empty_Interpolation_Still_Fails()
    {
        var builder = HtmlBuilder.Create();

        Assert.Empty(builder.Validate("<p>a {{- -}} b</p>"));
        Assert.NotEmpty(builder.Validate("<p>a {{ }} b</p>"));
    }

    private static string Text(string template, object model)
        => HtmlBuilder.Create().BuildFromTemplate(template, model, new TemplateOptions { Mode = TemplateMode.Text });

    private static string Html(string template, object model)
        => HtmlBuilder.Create().BuildFromTemplate(template, model);
}
