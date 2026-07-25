
using NgSharp;

namespace NgSharp.Tests.Parsing;

// Locks the hand-written {{ }} scanner (which replaced the compiled interpolation Regex) against the edge
// cases the old @"\{\{\s*(.*?)\s*\}\}" pattern defined — so the swap stays byte-identical.
public class InterpolationScannerTests
{
    [Fact]
    public void Interpolates_A_Single_Binding()
        => Assert.Equal("<p>Bob</p>", Render("<p>{{ Name }}</p>", new { Name = "Bob" }));

    [Fact]
    public void Interpolates_Adjacent_Bindings_With_No_Gap()
        => Assert.Equal("<p>AB</p>", Render("<p>{{ X }}{{ Y }}</p>", new { X = "A", Y = "B" }));

    [Fact]
    public void Trims_Inner_Whitespace_Like_The_Regex()
        => Assert.Equal("<p>Bob</p>", Render("<p>{{   Name   }}</p>", new { Name = "Bob" }));

    [Fact]
    public void Leaves_An_Unclosed_Opener_As_Literal_Text()
        => Assert.Equal("<p>{{ Name</p>", Render("<p>{{ Name</p>", new { Name = "Bob" }));

    [Fact]
    public void Does_Not_Interpolate_Across_A_Newline_In_The_Body()
    {
        // The old regex used '.' (no newline match), so a body spanning a line break stayed literal.
        var html = Render("<p>{{ Name\nExtra }}</p>", new { Name = "Bob" });
        Assert.Contains("{{ Name", html);
    }

    private static string Render(string tpl, object model) => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
