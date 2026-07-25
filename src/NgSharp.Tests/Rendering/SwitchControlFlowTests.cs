using System.Linq;

using NgSharp;

namespace NgSharp.Tests.Rendering;

// @switch / @case / @default (Angular 17 block syntax), both dialects: the value is evaluated ONCE,
// each @case compares with the expressions' '==' equality, the FIRST match renders, then @default,
// then nothing. Between the switch's braces only @case/@default and whitespace are legal — stray
// content is dropped by the lenient render and flagged by Validate.
public class SwitchControlFlowTests
{
    private static readonly TemplateOptions TextOptions = new TemplateOptions { Mode = TemplateMode.Text };

    // Rendering never minifies; normalize so assertions are whitespace-insensitive.
    private static string Render(string tpl, object model)
        => TestHtml.Minify(HtmlBuilder.Create().BuildFromTemplate(tpl, model));

    private static string Text(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model, TextOptions);

    #region HTML dialect

    [Fact]
    public void Switch_Takes_The_First_Matching_Case()
    {
        var content = Render(
            "<div>@switch (Status) { @case ('open') { <b>open</b> } @case ('done') { <i>done</i> } @default { <u>other</u> } }</div>",
            new { Status = "open" });

        Assert.Contains("<b>open</b>", content);
        Assert.DoesNotContain("<i>done</i>", content);
        Assert.DoesNotContain("<u>other</u>", content);
    }

    [Fact]
    public void Switch_Takes_A_Later_Case()
    {
        var content = Render(
            "<div>@switch (Status) { @case ('open') { <b>open</b> } @case ('done') { <i>done</i> } @default { <u>other</u> } }</div>",
            new { Status = "done" });

        Assert.Contains("<i>done</i>", content);
        Assert.DoesNotContain("<b>open</b>", content);
        Assert.DoesNotContain("<u>other</u>", content);
    }

    [Fact]
    public void Switch_Falls_Back_To_Default_When_Nothing_Matches()
    {
        var content = Render(
            "<div>@switch (Status) { @case ('open') { <b>open</b> } @case ('done') { <i>done</i> } @default { <u>other</u> } }</div>",
            new { Status = "archived" });

        Assert.Contains("<u>other</u>", content);
        Assert.DoesNotContain("<b>open</b>", content);
        Assert.DoesNotContain("<i>done</i>", content);
    }

    [Fact]
    public void Switch_Without_Match_And_Without_Default_Renders_Nothing()
    {
        var content = Render(
            "<div>@switch (N) { @case (1) { <b>one</b> } @case (2) { <i>two</i> } }</div>", new { N = 5 });

        Assert.Equal("<div></div>", content);
    }

    [Fact]
    public void First_Matching_Case_Wins_When_Cases_Duplicate()
    {
        var content = Render(
            "<div>@switch (N) { @case (1) { <b>first</b> } @case (1) { <i>second</i> } }</div>", new { N = 1 });

        Assert.Contains("<b>first</b>", content);
        Assert.DoesNotContain("<i>second</i>", content);
    }

    [Fact]
    public void Nested_Switch_Inside_A_Case()
    {
        var content = Render(
            "<div>@switch (Kind) { @case ('num') { @switch (N) { @case (1) { <b>one</b> } @default { <b>many</b> } } } @default { <u>none</u> } }</div>",
            new { Kind = "num", N = 1 });

        Assert.Contains("<b>one</b>", content);
        Assert.DoesNotContain("<b>many</b>", content);
        Assert.DoesNotContain("<u>none</u>", content);
    }

    [Fact]
    public void Switch_Inside_For_Reads_Loop_Variables_In_A_Case()
    {
        var content = Render(
            "<ul>@for (Items) { <li>@switch (Kind) { @case ('a') { <b>{{ $index }}-a</b> } @default { <i>{{ $index }}-x</i> } }</li> }</ul>",
            new { Items = new[] { new { Kind = "a" }, new { Kind = "b" } } });

        Assert.Contains("<li><b>0-a</b></li>", content);
        Assert.Contains("<li><i>1-x</i></li>", content);
    }

    [Fact]
    public void Stray_Content_Inside_Switch_Is_Ignored_At_Render()
    {
        var content = Render(
            "<div>@switch (N) { <p>stray</p> stray-text {{ N }} @case (1) { <b>one</b> } }</div>", new { N = 1 });

        Assert.Equal("<div><b>one</b></div>", content);
    }

    [Fact]
    public void Switch_Followed_By_Sibling_Content_Renders_The_Sibling()
    {
        var content = Render(
            "<div>@switch (N) { @case (1) { <b>one</b> } } <p>after</p></div>", new { N = 2 });

        Assert.DoesNotContain("<b>one</b>", content);
        Assert.Contains("<p>after</p>", content);
    }

    #endregion

    #region Case equality follows the '==' operator

    [Fact]
    public void Integer_Model_Value_Matches_A_Decimal_Case_Literal()
    {
        // Same rule as '==': numbers compare by value, so 1 and 1.0 are equal.
        var content = Render(
            "<div>@switch (N) { @case (1.0) { <b>hit</b> } @default { <i>miss</i> } }</div>", new { N = 1 });

        Assert.Contains("<b>hit</b>", content);
    }

    [Fact]
    public void Null_Model_Value_Matches_A_Null_Case_Literal()
    {
        var content = Render(
            "<div>@switch (Status) { @case (null) { <b>none</b> } @default { <i>some</i> } }</div>",
            new { Status = (string)null });

        Assert.Contains("<b>none</b>", content);
        Assert.DoesNotContain("<i>some</i>", content);
    }

    [Fact]
    public void Boolean_Model_Value_Matches_A_Boolean_Case_Literal()
    {
        var content = Render(
            "<div>@switch (Ok) { @case (true) { <b>yes</b> } @case (false) { <i>no</i> } }</div>", new { Ok = false });

        Assert.Contains("<i>no</i>", content);
        Assert.DoesNotContain("<b>yes</b>", content);
    }

    [Fact]
    public void A_Numeric_String_Never_Coerces_To_A_Number_Case()
    {
        // Same rule as '==': a string "1" and the number 1 differ in kind, so nothing matches.
        var content = Render(
            "<div>@switch (S) { @case (1) { <b>number</b> } @default { <i>string</i> } }</div>", new { S = "1" });

        Assert.Contains("<i>string</i>", content);
        Assert.DoesNotContain("<b>number</b>", content);
    }

    #endregion

    #region Text dialect

    [Fact]
    public void Text_Mode_Switch_Selects_The_Matching_Case()
        => Assert.Equal("Status: done.", Text(
            "Status: @switch (S) { @case ('open') {open} @case ('done') {done} @default {other} }.", new { S = "done" }));

    [Fact]
    public void Text_Mode_Switch_Falls_Back_To_Default()
        => Assert.Equal("Status: other.", Text(
            "Status: @switch (S) { @case ('open') {open} @case ('done') {done} @default {other} }.", new { S = "gone" }));

    [Fact]
    public void Text_Mode_Switch_Renders_Nothing_Without_Match_Or_Default()
        => Assert.Equal("Status: .", Text(
            "Status: @switch (S) { @case ('open') {open} }.", new { S = "gone" }));

    [Fact]
    public void Text_Mode_Switch_Inside_A_For_Loop()
        => Assert.Equal("[0:a][1:x]", Text(
            "@for (Items) {[{{ $index }}:@switch (Kind) { @case ('a') {a} @default {x} }]}",
            new { Items = new[] { new { Kind = "a" }, new { Kind = "b" } } }));

    [Fact]
    public void Text_Mode_Stray_Content_Inside_Switch_Is_Ignored_At_Render()
        => Assert.Equal("one", Text(
            "@switch (N) { stray {{ N }} @case (1) {one} }", new { N = 1 }));

    #endregion

    #region Whitespace control stays run-scoped

    [Fact]
    public void A_Trim_Marker_Inside_A_Case_Body_Never_Eats_Past_The_Switch()
    {
        // The '-}}' eater stops at the case body's sentinel barrier — ' TAIL' keeps its leading space.
        var output = Text("@switch (N) { @case (1) {one {{- Z -}}   } } TAIL", new { N = 1, Z = "z" });

        Assert.Equal("onez TAIL", output);
    }

    [Fact]
    public void A_Trim_Marker_After_The_Switch_Never_Reaches_Into_It()
    {
        // The '{{-' trims the whitespace between the switch's close and itself — never the case body.
        var output = Text("@switch (N) { @case (1) {one } }   {{- Z }}", new { N = 1, Z = "z" });

        Assert.Equal("one z", output);
    }

    #endregion

    #region Validation

    [Fact]
    public void Orphan_At_Case_Outside_A_Switch_Is_An_Error()
    {
        const string template = "<p>@case (1) { x }</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Orphan '@case'", error.Message);
        Assert.Equal(template.IndexOf("@case"), error.Position);
    }

    [Fact]
    public void Orphan_At_Default_Outside_A_Switch_Is_An_Error()
    {
        const string template = "<p>@default { x }</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Orphan '@default'", error.Message);
        Assert.Equal(template.IndexOf("@default"), error.Position);
    }

    [Fact]
    public void Text_Mode_Validation_Flags_An_Orphan_Case_Too()
    {
        const string template = "Status: @case (1) { x }";

        var diagnostics = HtmlBuilder.Create().Validate(template, TemplateMode.Text);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Orphan '@case'", error.Message);
        Assert.Equal(template.IndexOf("@case"), error.Position);
    }

    [Fact]
    public void Switch_Without_Any_Case_Is_A_Warning_At_The_Opener()
    {
        const string template = "<div>@switch (X) { }</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("without any '@case'", warning.Message);
        Assert.Equal(template.IndexOf("@switch"), warning.Position);
    }

    [Fact]
    public void Stray_Content_Inside_A_Switch_Is_An_Error_With_Position()
    {
        const string template = "<div>@switch (N) { <p>stray</p> @case (1) { <b>one</b> } }</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Stray content inside '@switch'", error.Message);
        Assert.Equal(template.IndexOf("<p>"), error.Position);
    }

    [Fact]
    public void Unclosed_Switch_Block_Is_An_Error_At_The_Opener()
    {
        const string template = "@switch (X) { @case (1) {<b>x</b>}";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Unclosed '@switch' block", error.Message);
        Assert.Equal(template.IndexOf("@switch"), error.Position);
    }

    #endregion

    #region Strict mode

    [Fact]
    public void Strict_Compile_Gate_Refuses_An_Orphan_Case()
    {
        var exception = Assert.Throws<NgSharpException>(
            () => HtmlBuilder.Create().Compile("<p>@case (1) { x }</p>", new TemplateOptions { Strict = true }));

        Assert.Contains("Orphan '@case'", exception.Message);
    }

    [Fact]
    public void Strict_Render_Of_A_Matchless_Switch_Renders_Empty()
    {
        // No match and no @default is a VALID outcome, not a strict error.
        var compiled = HtmlBuilder.Create().Compile(
            "<div>@switch (N) { @case (1) { <b>one</b> } }</div>", new TemplateOptions { Strict = true });

        Assert.Equal("<div></div>", TestHtml.Minify(compiled.Render(new { N = 5 })));
    }

    #endregion
}
