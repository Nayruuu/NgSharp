using System.Text.Json;

namespace NgSharp.Tests.Rendering;

// Opt-in strict rendering: a path that does not exist in the model throws NgSharpException with the
// path spelled out, while a property PRESENT with a null value still renders empty — and the default
// (non-strict) path keeps the historical lenient behavior byte-for-byte.
public class StrictModeTests
{
    private static readonly TemplateOptions StrictOptions = new TemplateOptions { Strict = true };

    #region Missing paths throw

    [Fact]
    public void Strict_Render_Throws_On_A_Missing_Path_Naming_It()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ MissingProp }}</p>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Name = "Alice" }));

        Assert.Contains("MissingProp", exception.Message);
        Assert.Contains("Strict mode", exception.Message);
    }

    [Fact]
    public void Strict_Render_Throws_On_A_Missing_Nested_Path_Under_A_Named_Loop_Variable()
    {
        var compiled = HtmlBuilder.Create().Compile("@for (p of Items) {<li>{{ p.Missing }}</li>}", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Items = new[] { new { Name = "a" } } }));

        Assert.Contains("p.Missing", exception.Message);
    }

    [Fact]
    public void Strict_Render_Throws_On_A_Missing_Path_In_A_Condition()
    {
        var compiled = HtmlBuilder.Create().Compile("<div [if]=\"MissingFlag\">x</div>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Ok = true }));

        Assert.Contains("MissingFlag", exception.Message);
    }

    [Fact]
    public void Strict_Render_Throws_On_A_Loop_Variable_Outside_Any_Loop()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ $index }}</p>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { }));

        Assert.Contains("$index", exception.Message);
    }

    [Fact]
    public void Strict_Text_Mode_Render_Throws_On_A_Missing_Path()
    {
        var builder = HtmlBuilder.Create();

        var exception = Assert.Throws<NgSharpException>(
            () => builder.BuildFromTemplate("Hello {{ Missing }}", new { Name = "x" }, new TemplateOptions { Mode = TemplateMode.Text, Strict = true }));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public void Strict_BuildFromTemplate_Throws_On_A_Missing_Path()
    {
        var builder = HtmlBuilder.Create();

        var exception = Assert.Throws<NgSharpException>(
            () => builder.BuildFromTemplate("<p>{{ Missing }}</p>", new { Name = "x" }, StrictOptions));

        Assert.Contains("Missing", exception.Message);
    }

    #endregion

    #region Present-but-null and guarded paths do not throw

    [Fact]
    public void Strict_Render_Does_Not_Throw_On_A_Present_Null_Value()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>", StrictOptions);

        Assert.Contains("<p></p>", compiled.Render(new { Name = (string)null }));
    }

    [Fact]
    public void Strict_Render_Does_Not_Throw_On_A_Path_Guarded_With_Optional_Chaining()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Missing?.Name }}</p>", StrictOptions);

        Assert.Contains("<p></p>", compiled.Render(new { Other = 1 }));
    }

    [Fact]
    public void Guarded_Paths_Still_Resolve_When_The_Data_Is_Present()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ User?.Name }}</p>", StrictOptions);

        Assert.Contains("<p>Alice</p>", compiled.Render(new { User = new { Name = "Alice" } }));
    }

    #endregion

    #region Non-boolean conditions throw (strict truthiness speaks)

    [Fact]
    public void Strict_If_Throws_When_The_Condition_Is_A_NonBoolean_Number()
    {
        // The Angular-dev trap: *ngIf="Count" is truthy there, silently falsy here.
        var compiled = HtmlBuilder.Create().Compile("<div [if]=\"Count\">x</div>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Count = 3 }));

        Assert.Contains("condition 'Count' evaluated to a non-boolean", exception.Message);
        Assert.Contains("strict truthiness: only real booleans are truthy", exception.Message);
    }

    [Fact]
    public void Strict_AtIf_Throws_When_The_Condition_Is_A_NonBoolean_String()
    {
        var compiled = HtmlBuilder.Create().Compile("@if (Name) {<p>x</p>}", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Name = "Alice" }));

        Assert.Contains("condition 'Name' evaluated to a non-boolean", exception.Message);
    }

    [Fact]
    public void Strict_If_Condition_Present_With_Null_Stays_Silently_Falsy()
    {
        // Only a non-boolean NON-NULL value throws: null-ish data keeps the absent-vs-null contract.
        var compiled = HtmlBuilder.Create().Compile("<b>@if (Value) {x} @else {y}</b>", StrictOptions);

        var html = compiled.Render(new { Value = (string?)null });

        Assert.Contains("y", html);
        Assert.DoesNotContain("x", html);
    }

    [Fact]
    public void Strict_If_Condition_Guarded_Missing_Path_Stays_Silently_Falsy()
    {
        var compiled = HtmlBuilder.Create().Compile("<div [if]=\"Extra?.Flag\">x</div>", StrictOptions);

        Assert.DoesNotContain("x", compiled.Render(new { Name = "a" }));
    }

    [Fact]
    public void Strict_If_Condition_Parseable_Boolean_String_Still_Renders()
    {
        var compiled = HtmlBuilder.Create().Compile("<div [if]=\"Flag\">x</div>", StrictOptions);

        Assert.Contains("x", compiled.Render(new { Flag = "true" }));
    }

    [Fact]
    public void NonStrict_If_NonBoolean_Condition_Stays_Silently_Falsy_Unchanged()
    {
        var compiled = HtmlBuilder.Create().Compile("<b><div [if]=\"Count\">x</div></b>");

        Assert.Equal("<b></b>", compiled.Render(new { Count = 3 }));
    }

    #endregion

    #region Division and modulo by zero throw (strict)

    [Fact]
    public void Strict_Division_By_Zero_Throws_Naming_The_Expression()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Total / Count }}</p>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Total = 10, Count = 0 }));

        Assert.Contains("division by zero", exception.Message);
        Assert.Contains("Total / Count", exception.Message);
    }

    [Fact]
    public void Strict_Modulo_By_Zero_Throws_Naming_The_Expression()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Total % Count }}</p>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Total = 10, Count = 0 }));

        Assert.Contains("modulo by zero", exception.Message);
        Assert.Contains("Total % Count", exception.Message);
    }

    [Fact]
    public void NonStrict_Division_And_Modulo_By_Zero_Still_Render_Zero()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Total / Count }}-{{ Total % Count }}</p>");

        Assert.Equal("<p>0-0</p>", compiled.Render(new { Total = 10, Count = 0 }));
    }

    [Fact]
    public void Strict_Division_By_A_NonZero_Divisor_Renders_Normally()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Total / Count }}</p>", StrictOptions);

        Assert.Equal("<p>5</p>", compiled.Render(new { Total = 10, Count = 2 }));
    }

    #endregion

    #region Strict compile gate (Validate errors throw)

    [Fact]
    public void Strict_Compile_Throws_On_A_Validation_Error()
    {
        var exception = Assert.Throws<NgSharpException>(
            () => HtmlBuilder.Create().Compile("@for (x in Items) {<li>{{ x }}</li>}", StrictOptions));

        Assert.Contains("did you mean", exception.Message);
        Assert.Contains("of", exception.Message);
    }

    [Fact]
    public void Strict_Compile_Accepts_A_Clean_Template_And_Sets_Strict()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>", StrictOptions);

        Assert.True(compiled.Strict);
        Assert.False(HtmlBuilder.Create().Compile("<p>{{ Name }}</p>").Strict);
    }

    [Fact]
    public void Strict_Compile_Is_Not_Blocked_By_Warnings()
    {
        // Unknown pipe is a Warning at validation (it may be registered later) — strict compiles;
        // reaching the pipe at render then throws, as it always has.
        var compiled = HtmlBuilder.Create().Compile("<p>{{ X | nope }}</p>", StrictOptions);

        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { X = "x" }));
        Assert.Contains("nope", exception.Message);
    }

    #endregion

    #region Non-strict stays lenient and outputs match

    [Fact]
    public void NonStrict_Render_Of_A_Missing_Path_Still_Renders_Empty()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Missing }}</p>");

        Assert.Contains("<p></p>", compiled.Render(new { Name = "x" }));
    }

    [Fact]
    public void Strict_And_NonStrict_Render_Identically_On_A_Sane_Template()
    {
        const string template =
            "<div>@for (p of Items) {<p class=\"row\">{{ p.Name | upper }} — {{ p.Price | number:'N2' }}</p>}" +
            "@if (Ok) {<b>{{ Title }}</b>} @else {<i>none</i>}</div>";
        var model = new
        {
            Items = new[] { new { Name = "café", Price = 12.5 }, new { Name = "thé", Price = 3.0 } },
            Ok = true,
            Title = "Menu <spécial>"
        };

        var builder = HtmlBuilder.Create();
        var lenient = builder.Compile(template).Render(model);
        var strict = builder.Compile(template, StrictOptions).Render(model);

        Assert.Equal(lenient, strict);
    }

    [Fact]
    public void Strict_Works_From_A_JsonElement_Model()
    {
        using var document = JsonDocument.Parse("{\"Name\":\"Alice\"}");
        var builder = HtmlBuilder.Create();

        Assert.Contains("<p>Alice</p>", builder.BuildFromTemplate("<p>{{ Name }}</p>", document.RootElement.Clone(), StrictOptions));

        var exception = Assert.Throws<NgSharpException>(
            () => builder.BuildFromTemplate("<p>{{ Missing }}</p>", document.RootElement.Clone(), StrictOptions));
        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public void Empty_Template_Throws_NgSharpException_With_The_Historical_Message()
    {
        var exception = Assert.Throws<NgSharpException>(() => HtmlBuilder.Create().Compile(""));

        Assert.Contains("Can't replace an empty html template", exception.Message);
    }

    #endregion
}
