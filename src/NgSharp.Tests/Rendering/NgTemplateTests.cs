
using NgSharp;

namespace NgSharp.Tests.Rendering;

// <ng-template #name> defines a reusable fragment (rendered nowhere inline); @render(name) / @render(name,
// context) instantiate it — the Angular ng-template model in NgSharp's @-block syntax.
public class NgTemplateTests
{
    [Fact]
    public void Render_Instantiates_A_Named_Template_Per_For_Item()
        // The current context inside the @for body is each item, so @render(row) renders it against that.
        => Assert.Contains(
            "<ul><li>a</li><li>b</li></ul>",
            Render(
                "<ng-template #row><li>{{ Name }}</li></ng-template><ul>@for (Items) {@render(row)}</ul>",
                new { Items = new[] { new { Name = "a" }, new { Name = "b" } } }));

    [Fact]
    public void Render_Passes_An_Explicit_Context()
        => Assert.Contains(
            "<p>Hi</p>",
            Render(
                "<ng-template #card><p>{{ Title }}</p></ng-template>@render(card, Header)",
                new { Header = new { Title = "Hi" } }));

    [Fact]
    public void Ng_Template_Definition_Is_Not_Rendered_Inline()
        => Assert.DoesNotContain(
            "NOPE",
            Render("<ng-template #x><b>NOPE</b></ng-template><p>ok</p>", new { Ok = true }));

    [Fact]
    public void Render_Of_An_Unknown_Template_Renders_Nothing()
        => Assert.Contains("<p></p>", Render("<p>@render(missing)</p>", new { Ok = true }));

    [Fact]
    public void A_Template_Can_Be_Rendered_In_Several_Places()
        => Assert.Contains(
            "<header>X</header><footer>X</footer>",
            Render(
                "<ng-template #brand>{{ Name }}</ng-template><header>@render(brand)</header><footer>@render(brand)</footer>",
                new { Name = "X" }));

    private static string Render(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
