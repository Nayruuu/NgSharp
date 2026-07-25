using System.Linq;

namespace NgSharp.Tests.Validation;

// HtmlBuilder.Validate: every construct today's lenient renderer swallows silently must come back as
// a diagnostic with a usable position — WITHOUT throwing, and without changing what a plain parse
// or render produces (the collector is ambient and validation-only).
public class ValidationTests
{
    #region Interpolations

    [Fact]
    public void Unclosed_Interpolation_Is_An_Error_At_The_Exact_Position()
    {
        const string template = "<p>{{ Nom</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Unclosed interpolation", error.Message);
        Assert.Equal(template.IndexOf("{{"), error.Position);
    }

    [Fact]
    public void Empty_Interpolation_Is_An_Error()
    {
        const string template = "<p>{{ }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Empty interpolation", error.Message);
        Assert.Equal(template.IndexOf("{{"), error.Position);
    }

    [Fact]
    public void Interpolation_Spanning_A_Line_Break_Is_A_Warning()
    {
        const string template = "<p>{{ Na\nme }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("line break", warning.Message);
        Assert.Equal(template.IndexOf("{{"), warning.Position);
    }

    #endregion

    #region Control flow

    [Fact]
    public void For_In_Instead_Of_Of_Is_An_Explicit_Error()
    {
        const string template = "<ul>@for (x in Items) {<li>{{ x }}</li>}</ul>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var forError = diagnostics.First(d => d.Message.Contains("did you mean"));
        Assert.Equal(DiagnosticSeverity.Error, forError.Severity);
        Assert.Contains("@for (x of Items)", forError.Message);
        Assert.Equal(template.IndexOf("@for"), forError.Position);
    }

    [Fact]
    public void Unclosed_At_If_Block_Is_An_Error_At_The_Opener()
    {
        const string template = "<div>@if (Ok) {<p>x</p></div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Unclosed '@if' block", error.Message);
        Assert.Equal(template.IndexOf("@if"), error.Position);
    }

    [Fact]
    public void Unclosed_At_For_Block_Is_An_Error_At_The_Opener()
    {
        const string template = "@for (p of Items) {<li>{{ p }}</li>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Contains("Unclosed '@for' block", error.Message);
        Assert.Equal(0, error.Position);
    }

    [Fact]
    public void Orphan_At_Else_Is_An_Error()
    {
        const string template = "<p>a</p>@else {<p>b</p>}";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = diagnostics.First(d => d.Message.Contains("@else"));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Orphan '@else'", error.Message);
        Assert.Equal(template.IndexOf("@else"), error.Position);
    }

    [Fact]
    public void Orphan_Else_Attribute_Is_An_Error()
    {
        const string template = "<p>a</p><div [else]>never</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("[else]", error.Message);
        Assert.Equal(template.IndexOf("<div"), error.Position);
    }

    [Fact]
    public void Chained_Else_Attribute_After_An_If_Is_Not_Flagged()
    {
        const string template = "<div [if]=\"Ok\">a</div><div [else]>b</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        Assert.Empty(diagnostics);
    }

    #endregion

    #region Expressions and pipes

    [Fact]
    public void Missing_Pipe_Name_Is_An_Error()
    {
        const string template = "<p>{{ X | }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Missing pipe name", error.Message);
        Assert.Equal(template.IndexOf("X |"), error.Position);
    }

    [Fact]
    public void Pipe_Argument_Missing_After_Colon_Is_An_Error()
    {
        const string template = "<p>{{ Total | number: }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Unexpected end of expression", error.Message);
        Assert.Contains("Total | number:", error.Message);
    }

    [Fact]
    public void Unknown_Pipe_Is_A_Warning_Naming_The_Pipe()
    {
        const string template = "<p>{{ X | nope }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("Unknown pipe 'nope'", warning.Message);
    }

    [Fact]
    public void Registered_Custom_Pipe_Is_Not_Flagged()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe<ShoutPipe>();

        Assert.Empty(builder.Validate("<p>{{ X | shout }}</p>"));
    }

    [Fact]
    public void Unparsable_Expression_Tail_Is_An_Error()
    {
        const string template = "<p>{{ a b }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Unexpected 'b'", error.Message);
        Assert.Equal(template.IndexOf("a b"), error.Position);
    }

    [Fact]
    public void Empty_Binding_Expression_Is_An_Error()
    {
        const string template = "<div [if]=\"\">x</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Empty expression", error.Message);
    }

    #endregion

    #region Positions, dialects, and non-interference

    [Fact]
    public void Positions_Map_Back_Through_The_At_Block_Desugaring()
    {
        // The '{{' sits AFTER an @if opener, so its offset in the desugared text differs from the
        // source — the published position must still point at the source '{{'.
        const string template = "@if (Ok) { {{ Nom }";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var unclosed = diagnostics.First(d => d.Message.Contains("Unclosed interpolation"));
        Assert.Equal(template.IndexOf("{{"), unclosed.Position);
        Assert.Contains(diagnostics, d => d.Message.Contains("Unclosed '@if' block") && d.Position == 0);
    }

    [Fact]
    public void Text_Mode_Detects_The_Same_Families()
    {
        var builder = HtmlBuilder.Create();

        var unclosed = builder.Validate("Hello {{ Name", TemplateMode.Text);
        Assert.Contains(unclosed, d => d.Message.Contains("Unclosed interpolation") && d.Position == 6);

        var forIn = builder.Validate("@for (x in Items) {{{ x }}}", TemplateMode.Text);
        Assert.Contains(forIn, d => d.Message.Contains("did you mean"));

        var unclosedBlock = builder.Validate("@if (Ok) {yes", TemplateMode.Text);
        Assert.Contains(unclosedBlock, d => d.Message.Contains("Unclosed '@if' block"));
    }

    [Fact]
    public void Empty_Template_Is_A_Diagnostic_Not_A_Throw()
    {
        var diagnostics = HtmlBuilder.Create().Validate("");

        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("empty html template", error.Message);
    }

    [Fact]
    public void A_Clean_Template_Yields_No_Diagnostics()
    {
        const string template =
            "<div>@for (p of Items) {<p>{{ p.Name | upper }} #{{ $index }}</p>}" +
            "@if (Ok) {<b>ok</b>} @else {<i>no</i>}</div>";

        Assert.Empty(HtmlBuilder.Create().Validate(template));
    }

    [Fact]
    public void Validate_Leaves_Normal_Parsing_And_Rendering_Untouched()
    {
        var builder = HtmlBuilder.Create();

        // A validation pass with findings must not leak its collector into later parses/renders.
        Assert.NotEmpty(builder.Validate("<p>{{ Nom</p>"));

        var content = builder.BuildFromTemplate("<p>{{ Nom</p>", new { Nom = "x" });
        Assert.Contains("{{ Nom", content);   // lenient behavior: literal, unchanged

        var clean = builder.BuildFromTemplate("<p>{{ Nom }}</p>", new { Nom = "x" });
        Assert.Contains("<p>x</p>", clean);
    }

    [Fact]
    public void The_Big_EndToEnd_Template_Validates_Without_Errors()
    {
        // A real production-shaped document (components, pipes, control flow, <style> blocks) must
        // produce ZERO Error diagnostics — Validate must never cry wolf on a healthy template.
        var template = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Templates", "big-test.html"));

        var diagnostics = HtmlBuilder.Create().Validate(template);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Diagnostics_Are_Ordered_By_Position()
    {
        const string template = "<p>{{ a b }}</p><p>{{ X | }}</p><i>{{ Fin</i>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        Assert.True(diagnostics.Count >= 3);
        Assert.Equal(diagnostics.OrderBy(d => d.Position).Select(d => d.Message), diagnostics.Select(d => d.Message));
    }

    #endregion

    #region Unregistered dashed tags (components) are warnings

    [Fact]
    public void Unregistered_Dashed_Tag_Is_A_Warning_Pointing_At_The_Tag()
    {
        const string template = "<div><user-card></user-card></div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("if '<user-card>' is a component, register it before Compile", warning.Message);
        Assert.Equal(template.IndexOf("<user-card"), warning.Position);
    }

    [Fact]
    public void Registered_Component_Dashed_Tag_Is_Clean()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<UserCardComponent>();

        Assert.Empty(builder.Validate("<div><user-card></user-card></div>"));
    }

    [Fact]
    public void Ng_Template_And_Ng_Container_Are_Never_Flagged()
    {
        const string template = "<ng-container><ng-template #frag><b>x</b></ng-template></ng-container>";

        Assert.Empty(HtmlBuilder.Create().Validate(template));
    }

    [Fact]
    public void Dashed_Tag_Warnings_Skip_Comments_And_Report_Each_Name_Once()
    {
        const string template = "<!-- <old-tag></old-tag> --><w-z>a</w-z><w-z>b</w-z>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Contains("'<w-z>'", warning.Message);
        Assert.Equal(template.IndexOf("<w-z"), warning.Position);
    }

    [Fact]
    public void Dashed_Tags_Are_Not_Flagged_In_Text_Mode()
    {
        // The Text dialect never reads markup — '<x-y>' is plain text there.
        Assert.Empty(HtmlBuilder.Create().Validate("value: <x-y>", TemplateMode.Text));
    }

    #endregion

    #region Always-false conditions and literal zero divisors are warnings

    [Fact]
    public void Null_Literal_If_Condition_Is_An_Always_False_Warning()
    {
        var diagnostics = HtmlBuilder.Create().Validate("<div [if]=\"null\">never</div>");

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("statically null", warning.Message);
    }

    [Fact]
    public void String_Literal_If_Condition_Is_An_Always_False_Warning_At_The_Tag()
    {
        const string template = "<div [if]=\"'draft'\">never</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("Always-false condition", warning.Message);
        Assert.Contains("'draft'", warning.Message);
        Assert.Contains("a string", warning.Message);
        Assert.Contains("the body never renders", warning.Message);
        Assert.Equal(template.IndexOf("<div"), warning.Position);
    }

    [Fact]
    public void Number_Literal_At_If_Condition_Warns_In_Both_Dialects()
    {
        var builder = HtmlBuilder.Create();

        var html = builder.Validate("@if (42) {<b>x</b>}");
        var htmlWarning = Assert.Single(html);
        Assert.Equal(DiagnosticSeverity.Warning, htmlWarning.Severity);
        Assert.Contains("'42'", htmlWarning.Message);
        Assert.Contains("a number", htmlWarning.Message);
        Assert.Equal(0, htmlWarning.Position);

        var text = builder.Validate("@if (42) {x}", TemplateMode.Text);
        var textWarning = Assert.Single(text);
        Assert.Equal(DiagnosticSeverity.Warning, textWarning.Severity);
        Assert.Contains("a number", textWarning.Message);
    }

    [Fact]
    public void Arithmetic_If_Condition_Warns_As_Always_Numeric()
    {
        const string template = "<p [if]=\"Count - 1\">x</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("'Count - 1'", warning.Message);
        Assert.Contains("numeric", warning.Message);
    }

    [Fact]
    public void Else_If_Condition_Warns_Too()
    {
        const string template = "<div [if]=\"Ok\">a</div><div [else-if]=\"5\">b</div>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("'5'", warning.Message);
    }

    [Fact]
    public void Path_Comparison_Ternary_And_Coercible_Conditions_Are_Not_Flagged()
    {
        var builder = HtmlBuilder.Create();

        // A path is unknowable; comparison/'!'/logical are boolean; a boolean-branch ternary is
        // boolean; a '+' with a possible string operand may concatenate; 'true' coerces.
        Assert.Empty(builder.Validate("<div [if]=\"Ready\">a</div>"));
        Assert.Empty(builder.Validate("@if (Count > 0) {<b>x</b>}"));
        Assert.Empty(builder.Validate("@if (!Archived && Active) {<b>x</b>}"));
        Assert.Empty(builder.Validate("<div [if]=\"Vip ? true : false\">a</div>"));
        Assert.Empty(builder.Validate("<div [if]=\"Count + 1\">a</div>"));
        Assert.Empty(builder.Validate("<div [if]=\"'true'\">a</div>"));
    }

    [Fact]
    public void Division_By_Literal_Zero_Is_A_Warning_At_The_Expression()
    {
        const string template = "<p>{{ Total / 0 }}</p>";

        var diagnostics = HtmlBuilder.Create().Validate(template);

        var warning = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("Division by the literal 0", warning.Message);
        Assert.Contains("Total / 0", warning.Message);
        Assert.Equal(template.IndexOf("Total"), warning.Position);
    }

    [Fact]
    public void Modulo_By_Literal_Zero_Warns_Including_The_Double_Form()
    {
        var builder = HtmlBuilder.Create();

        var modulo = Assert.Single(builder.Validate("<p>{{ Total % 0 }}</p>"));
        Assert.Contains("Modulo by the literal 0", modulo.Message);

        var doubleZero = Assert.Single(builder.Validate("<p>{{ Total / 0.0 }}</p>", TemplateMode.Text));
        Assert.Contains("Division by the literal 0", doubleZero.Message);
    }

    [Fact]
    public void NonZero_Or_NonLiteral_Divisors_Are_Clean()
    {
        var builder = HtmlBuilder.Create();

        Assert.Empty(builder.Validate("<p>{{ Total / 2 }}</p>"));
        Assert.Empty(builder.Validate("<p>{{ Total % Count }}</p>"));
        Assert.Empty(builder.Validate("<p>{{ Total * 0 }}</p>"));
    }

    [Fact]
    public void Lenient_Rendering_Of_The_Flagged_Constructs_Is_Unchanged()
    {
        var builder = HtmlBuilder.Create();
        var model = new { Total = 10 };

        // The warnings describe exactly what lenient does: always-false condition -> the else branch;
        // division by zero -> a plain 0.
        Assert.Equal("<i>else</i>", builder.BuildFromTemplate("<b [if]=\"'draft'\">body</b><i [else]=\"\">else</i>", model));
        Assert.Equal("<p>0</p>", builder.BuildFromTemplate("<p>{{ Total / 0 }}</p>", model));
    }

    #endregion

    private sealed class ShoutPipe : NgSharp.Pipes.IPipe
    {
        public string PipeName => "shout";

        public string Transform(string tagName, NgElement value, string argument)
            => (value.GetString() ?? string.Empty).ToUpperInvariant() + "!";
    }

    private sealed class UserCardComponent : NgSharp.Components.IComponent
    {
        public string ComponentName => "user-card";

        public string Render() => "<div class=\"card\"></div>";
    }
}
