using System.Globalization;

namespace NgSharp.Tests;

// Regression + edge-case tests born from the audit.
//
// [Fact]                  -> behaviour that is CORRECT today; these guard against regressions.
// [Fact(Skip = "...")]    -> behaviour that is KNOWN BROKEN today; each asserts the DESIRED
//                            result and carries the reason. Remove the Skip when the bug is fixed
//                            and the test should turn green on its own.
public class RegressionTests
{
    private static HtmlBuilder Builder => HtmlBuilder.Default;

    private static Task<string> Render(string template, object model)
        => Builder.BuildFromTemplateAsync(template, model);

    #region Structural conditions ( [if] with == / != )

    [Fact]
    public async Task If_NumericEquality_True_KeepsElement()
    {
        var content = await Render("<div><p [if]=\"Count == 3\">KEPT</p></div>", new { Count = 3 });

        Assert.Contains("<p>KEPT</p>", content);
    }

    [Fact]
    public async Task If_NumericEquality_False_RemovesElement()
    {
        var content = await Render("<div><p [if]=\"Count == 5\">KEPT</p></div>", new { Count = 3 });

        Assert.DoesNotContain("KEPT", content);
    }

    [Fact]
    public async Task If_NumericInequality_True_KeepsElement()
    {
        var content = await Render("<div><p [if]=\"Count != 5\">KEPT</p></div>", new { Count = 3 });

        Assert.Contains("<p>KEPT</p>", content);
    }

    [Fact]
    public async Task If_NumericInequality_False_RemovesElement()
    {
        var content = await Render("<div><p [if]=\"Count != 3\">KEPT</p></div>", new { Count = 3 });

        Assert.DoesNotContain("KEPT", content);
    }

    [Fact]
    public async Task If_StringEquality_True_KeepsElement()
    {
        var content = await Render("<div><p [if]=\"Status == 'active'\">KEPT</p></div>", new { Status = "active" });

        Assert.Contains("<p>KEPT</p>", content);
    }

    [Fact]
    public async Task If_StringInequality_SameValue_RemovesElement()
    {
        var content = await Render("<div><p [if]=\"Status != 'active'\">KEPT</p></div>", new { Status = "active" });

        Assert.DoesNotContain("KEPT", content);
    }

    [Fact]
    public async Task If_NotNull_WithValue_KeepsElement()
    {
        var content = await Render("<div><p [if]=\"Name != null\">KEPT</p></div>", new { Name = "x" });

        Assert.Contains("<p>KEPT</p>", content);
    }

    [Fact]
    public async Task If_NotNull_WithNull_RemovesElement()
    {
        var content = await Render("<div><p [if]=\"Name != null\">KEPT</p></div>", new { Name = (string?)null });

        Assert.DoesNotContain("KEPT", content);
    }

    [Fact]
    public async Task If_EqualsNull_WithNull_KeepsElement()
    {
        var content = await Render("<div><p [if]=\"Name == null\">KEPT</p></div>", new { Name = (string?)null });

        Assert.Contains("<p>KEPT</p>", content);
    }

    #endregion

    #region Interpolation ( {{ ... }} , with and without a pipe )

    [Fact]
    public async Task Pipe_Interpolation_Keeps_The_Text_Around_It()
    {
        var content = await Render("<div><p>Hello {{ Name | upper }}, welcome!</p></div>", new { Name = "alice" });

        Assert.Contains("<p>Hello ALICE, welcome!</p>", content);
    }

    [Fact]
    public async Task Two_Piped_Interpolations_Same_Pipe_In_One_Node_Both_Render()
    {
        var content = await Render("<div><p>{{ First | upper }} and {{ Second | upper }}</p></div>",
            new { First = "alice", Second = "bob" });

        Assert.Contains("<p>ALICE and BOB</p>", content);
    }

    [Fact]
    public async Task Two_Plain_Interpolations_In_One_Node_Both_Render()
    {
        var content = await Render("<div><p>{{ A }}-{{ B }}</p></div>", new { A = "x", B = "y" });

        Assert.Contains("<p>x-y</p>", content);
    }

    [Fact]
    public async Task Missing_Property_Renders_Empty()
    {
        var content = await Render("<div><p>{{ DoesNotExist }}</p></div>", new { Name = "x" });

        Assert.Contains("<p></p>", content);
    }

    [Fact]
    public async Task Pipe_With_Argument_Formats_The_Value()
    {
        var content = await Render("<div><p>{{ D | date: 'yyyy' }}</p></div>", new { D = new DateTime(2023, 5, 1) });

        Assert.Contains("<p>2023</p>", content);
    }

    [Fact]
    public async Task Number_Pipe_On_Null_Renders_Zero()
    {
        var content = await Render("<div><p>{{ N | number: 'N0' }}</p></div>", new { N = (int?)null });

        Assert.Contains("<p>0</p>", content);
    }

    [Fact]
    public async Task Number_Pipe_Respects_The_Format_And_Culture()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var content = await Render("<div><p>{{ N | number: 'N0' }}</p></div>", new { N = 12345 });

        Assert.Contains("<p>12,345</p>", content);
    }

    #endregion

    #region Security ( interpolation must neutralize HTML injection )

    [Fact]
    public async Task Plain_Interpolation_Escapes_Html_Injection()
    {
        var content = await Render("<div><p>{{ X }}</p></div>", new { X = "<script>alert(1)</script>" });

        Assert.DoesNotContain("<script>", content);
        Assert.Contains("&lt;script&gt;", content);
    }

    #endregion

    #region ReDoS ( a malformed directive-shaped attribute name must not hang the parser )

    [Fact]
    public async Task Malformed_Bracket_Attribute_Name_Does_Not_Hang()
    {
        // A '[' -prefixed attribute name that never closes its ']' used to trigger
        // catastrophic backtracking in the directive-name regex: ~6 s at 26 chars,
        // ~24 s at 28, non-terminating beyond. The parser must stay near-instant.
        var evilName = new string('a', 26);
        var template = $"<div><p [{evilName}=\"x\">content</p></div>";

        var render = Task.Run(() => Render(template, new { x = 1 }));
        var winner = await Task.WhenAny(render, Task.Delay(TimeSpan.FromSeconds(3)));

        Assert.True(winner == render, "Rendering a malformed '[' attribute name hung (regex backtracking).");

        var content = await render;
        Assert.Contains("content", content);
    }

    #endregion

    #region Known gaps ( skipped: assert the DESIRED behaviour, remove Skip once fixed )

    [Fact]
    public async Task Ternary_In_Condition_Selects_The_Right_Branch()
    {
        var whenTrue = await Render("<div><p [attr.class]=\"Ok == true ? 'yes' : 'no'\">x</p></div>", new { Ok = true });
        var whenFalse = await Render("<div><p [attr.class]=\"Ok == true ? 'yes' : 'no'\">x</p></div>", new { Ok = false });

        Assert.Contains("class=\"yes\"", whenTrue);
        Assert.Contains("class=\"no\"", whenFalse);
    }

    [Fact]
    public async Task For_Loop_Renders_Interpolation_In_Direct_Text_Child()
    {
        var content = await Render("<ul><li [for]=\"Items\">{{ Label }}</li></ul>",
            new { Items = new[] { new { Label = "a" }, new { Label = "b" } } });

        Assert.Contains("<li>a</li>", content);
        Assert.Contains("<li>b</li>", content);
    }

    [Fact]
    public async Task Two_Different_Pipes_In_One_Node_Both_Render()
    {
        var content = await Render("<div><p>{{ Name | upper }} scored {{ Score | number: 'N0' }}</p></div>",
            new { Name = "alice", Score = 1234 });

        Assert.Contains("ALICE", content);
        Assert.Contains("1", content);
    }

    [Fact]
    public async Task Unknown_Pipe_Fails_With_A_Clear_Error()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => Render("<div><p>{{ X | nope }}</p></div>", new { X = "x" }));

        Assert.Contains("nope", ex.Message);
        Assert.IsNotType<KeyNotFoundException>(ex);
    }

    #endregion

    #region JsonElement overload ( reflection-free model ingestion, AOT / script friendly )

    [Fact]
    public async Task Renders_From_A_JsonElement_Model_Without_Reflection_Serialization()
    {
        // A JsonElement built by the parser (no reflection) must render directly, so the engine
        // stays usable where reflection-based System.Text.Json serialization is disabled
        // (Native AOT, trimming, .NET file-based scripts).
        using var doc = System.Text.Json.JsonDocument.Parse("{\"Name\":\"alice\",\"Score\":42}");

        var content = await Builder.BuildFromTemplateAsync("<p>{{ Name | upper }}:{{ Score }}</p>", doc.RootElement);

        Assert.Contains("<p>ALICE:42</p>", content);
    }

    #endregion

    #region Logical operators end-to-end ( [if] and @if with && / || )

    [Fact]
    public async Task If_With_And_Keeps_When_Both_True()
    {
        var content = await Render("<div><p [if]=\"A == 1 && B == 2\">X</p></div>", new { A = 1, B = 2 });

        Assert.Contains("<p>X</p>", content);
    }

    [Fact]
    public async Task If_With_And_Drops_When_Second_False()
    {
        var content = await Render("<div><p [if]=\"A == 1 && B == 9\">X</p></div>", new { A = 1, B = 2 });

        Assert.DoesNotContain("X", content);
    }

    [Fact]
    public async Task If_With_Or_Keeps_When_One_True()
    {
        var content = await Render("<div><p [if]=\"A == 9 || B == 2\">X</p></div>", new { A = 9, B = 2 });

        Assert.Contains("<p>X</p>", content);
    }

    [Fact]
    public async Task If_With_Or_Drops_When_Both_False()
    {
        var content = await Render("<div><p [if]=\"A == 9 || B == 8\">X</p></div>", new { A = 1, B = 2 });

        Assert.DoesNotContain("X", content);
    }

    [Fact]
    public async Task If_With_Parens_And_Precedence_Keeps()
    {
        var content = await Render("<div><p [if]=\"(A == 1 || B == 9) && C == 3\">X</p></div>", new { A = 1, B = 2, C = 3 });

        Assert.Contains("<p>X</p>", content);
    }

    [Fact]
    public async Task If_With_Parens_And_Precedence_Drops_When_Trailing_And_False()
    {
        var content = await Render("<div><p [if]=\"(A == 1 || B == 9) && C == 9\">X</p></div>", new { A = 1, B = 2, C = 3 });

        Assert.DoesNotContain("X", content);
    }

    [Fact]
    public async Task AtIf_ControlFlow_With_And_Keeps_When_Both_True()
    {
        var content = await Render("<div>@if (A == 1 && B == 2) { <p>X</p> }</div>", new { A = 1, B = 2 });

        Assert.Contains("<p>X</p>", content);
    }

    [Fact]
    public async Task AtIf_ControlFlow_With_And_Drops_When_One_False()
    {
        var content = await Render("<div>@if (A == 1 && B == 9) { <p>X</p> }</div>", new { A = 1, B = 2 });

        Assert.DoesNotContain("X", content);
    }

    #endregion
}
