
using NgSharp;

namespace NgSharp.Tests.Rendering;

// End-to-end (through HtmlBuilder) coverage for unary '!' and safe-navigation '?.' in real templates.
public class UnaryNotAndSafeNavRenderingTests
{
    [Fact]
    public void If_With_Not_Renders_The_Element_When_Flag_Is_False()
        => Assert.Contains("shown", Render("<p [if]=\"!Hidden\">shown</p>", new { Hidden = false }));

    [Fact]
    public void If_With_Not_Drops_The_Element_When_Flag_Is_True()
        => Assert.DoesNotContain("shown", Render("<p [if]=\"!Hidden\">shown</p>", new { Hidden = true }));

    [Fact]
    public void SafeNav_Renders_A_Present_Member()
        => Assert.Contains("<p>Alice</p>", Render("<p>{{ User?.Name }}</p>", new { User = new { Name = "Alice" } }));

    [Fact]
    public void SafeNav_On_A_Null_Intermediate_Renders_Empty()
        => Assert.Contains("<p></p>", Render("<p>{{ User?.Name }}</p>", new { User = (string)null }));

    [Fact]
    public void SafeNav_Chained_Renders_A_Deep_Member()
        => Assert.Contains("<span>Paris</span>", Render("<span>{{ User?.Address?.City }}</span>", new { User = new { Address = new { City = "Paris" } } }));

    [Fact]
    public void Array_Index_In_An_Interpolation_Renders_The_Element()
        // Locks the README's "array indices" support claim.
        => Assert.Contains("<p>Bob</p>", Render("<p>{{ Users[1].Name }}</p>", new { Users = new[] { new { Name = "Alice" }, new { Name = "Bob" } } }));

    private static string Render(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
