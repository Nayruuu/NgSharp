
using NgSharp;

namespace NgSharp.Tests.Rendering;

// A bare Angular property binding — '[src]="Url"' — binds like '[attr.src]': the expression is evaluated
// and set as a real attribute (omitted when the value is null), while structural directives ([if]/[for]/…),
// the prefixed bindings ([attr.x]/[style.x]/[class.x]) and registered custom directives are unaffected.
public class BarePropertyBindingTests
{
    [Fact]
    public void Bare_Property_Binding_Sets_A_Real_Attribute()
        // The leading space proves it is a real 'src="..."' attribute, not the literal '[src]="..."'.
        => Assert.Contains(" src=\"a.jpg\"", Render("<img [src]=\"Url\">", new { Url = "a.jpg" }));

    [Fact]
    public void Bare_Property_Binding_Does_Not_Leak_The_Bracketed_Literal()
        => Assert.DoesNotContain("[src]", Render("<img [src]=\"Url\">", new { Url = "a.jpg" }));

    [Fact]
    public void Bare_Property_Binding_Omits_The_Attribute_When_Null()
        => Assert.DoesNotContain("src=", Render("<img [src]=\"Url\">", new { Url = (string)null }));

    [Fact]
    public void Bare_Property_Binding_Evaluates_The_Expression()
        => Assert.Contains("href=\"/home\"", Render("<a [href]=\"Ok == true ? '/home' : '/away'\">x</a>", new { Ok = true }));

    private static string Render(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
