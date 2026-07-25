
using NgSharp;

namespace NgSharp.Tests.Rendering;

// Explicit @for (p of X) loop variables (Angular-17 syntax): the item binds to the named var, and a named
// frame is reachable ONLY by its var — a bare name resolves to the outer/root scope (Angular semantics),
// unlike the classic implicit @for/[for] where a bare name resolves on the loop item.
public class NgForExplicitVarTests
{
    [Fact]
    public void Explicit_Variable_Binds_The_Item()
        => Assert.Equal("<ul><li>A</li><li>B</li></ul>",
            Render("<ul>@for (p of Items) {<li>{{ p.Name }}</li>}</ul>",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" } } }));

    [Fact]
    public void Nested_Explicit_Variables_Reach_The_Outer_Item()
        => Assert.Equal("<div>X:1</div><div>X:2</div><div>Y:3</div>",
            Render("@for (c of Cats) {@for (n of c.Nums) {<div>{{ c.Name }}:{{ n.V }}</div>}}",
                new
                {
                    Cats = new[]
                    {
                        new { Name = "X", Nums = new[] { new { V = 1 }, new { V = 2 } } },
                        new { Name = "Y", Nums = new[] { new { V = 3 } } },
                    },
                }));

    [Fact]
    public void Bare_Name_Resolves_Outer_Not_The_Named_Item()
        => Assert.Equal("<p>T:A</p>",
            Render("@for (p of Items) {<p>{{ Title }}:{{ p.Name }}</p>}",
                new { Title = "T", Items = new[] { new { Name = "A" } } }));

    [Fact]
    public void Angular_Track_Clause_Is_Ignored()
        => Assert.Equal("<i>A</i><i>B</i>",
            Render("@for (p of Items; track p.Name) {<i>{{ p.Name }}</i>}",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" } } }));

    [Fact]
    public void Classic_Implicit_For_Still_Resolves_Bare_Names_On_The_Item()
        => Assert.Equal("<li>A</li><li>B</li>",
            Render("@for (Items) {<li>{{ Name }}</li>}",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" } } }));

    private static string Render(string tpl, object model) => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
