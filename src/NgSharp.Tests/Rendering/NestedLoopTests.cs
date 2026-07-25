
using NgSharp;

namespace NgSharp.Tests.Rendering;

// Nested [for]/@for scope resolution: an inner loop must see its own item's fields first, then fall
// back up the parent chain to the outer item(s) and the root (ExpressionEvaluator.ResolvePath walks
// NgElement.Parent).
public class NestedLoopTests
{
    [Fact]
    public void Nested_For_Renders_Inner_Items()
    {
        var model = new { Rows = new[] { new { Cells = new[] { new { V = "a" }, new { V = "b" } } } } };

        var content = Render("<table><tr [for]=\"Rows\"><td [for]=\"Cells\">{{ V }}</td></tr></table>", model);

        Assert.Contains("<td>a</td>", content);
        Assert.Contains("<td>b</td>", content);
    }

    [Fact]
    public void Inner_Loop_Sees_Outer_Scope_Field()
    {
        var model = new
        {
            Categories = new[]
            {
                new { Name = "Fruits", Products = new[] { new { Title = "Apple" }, new { Title = "Pear" } } },
                new { Name = "Veg", Products = new[] { new { Title = "Carrot" } } }
            }
        };

        var content = Render("<div [for]=\"Categories\"><span [for]=\"Products\">{{ Name }}:{{ Title }};</span></div>", model);

        Assert.Contains("Fruits:Apple;", content);
        Assert.Contains("Fruits:Pear;", content);
        Assert.Contains("Veg:Carrot;", content);
    }

    [Fact]
    public void Inner_Field_Shadows_Outer_Same_Name()
    {
        var model = new { Outer = new[] { new { Id = "OUT", Inner = new[] { new { Id = "IN1" }, new { Id = "IN2" } } } } };

        var content = Render("<div [for]=\"Outer\">o={{ Id }};<span [for]=\"Inner\">i={{ Id }};</span></div>", model);

        Assert.Contains("o=OUT;", content);
        Assert.Contains("i=IN1;", content);
        Assert.Contains("i=IN2;", content);
        Assert.DoesNotContain("i=OUT;", content); // inner Id must win, not the outer
    }

    [Fact]
    public void Three_Level_Nested_For_Resolves_All_Scopes()
    {
        var model = new { A = new[] { new { X = "1", B = new[] { new { Y = "2", C = new[] { new { Z = "3" } } } } } } };

        var content = Render("<div [for]=\"A\"><div [for]=\"B\"><span [for]=\"C\">{{ X }}-{{ Y }}-{{ Z }}</span></div></div>", model);

        Assert.Contains("<span>1-2-3</span>", content);
    }

    [Fact]
    public void Inner_Loop_Sees_Root_Field()
    {
        var model = new { Currency = "EUR", Items = new[] { new { Sub = new[] { new { Price = 10 } } } } };

        var content = Render("<div [for]=\"Items\"><span [for]=\"Sub\">{{ Price }} {{ Currency }}</span></div>", model);

        Assert.Contains("10 EUR", content);
    }

    [Fact]
    public void Nested_For_With_Inner_If_Over_Outer_And_Inner_Fields()
    {
        var model = new
        {
            Groups = new[]
            {
                new { Active = true, Items = new[] { new { Name = "keep" } } },
                new { Active = false, Items = new[] { new { Name = "drop" } } }
            }
        };

        var content = Render("<div [for]=\"Groups\"><span [for]=\"Items\" [if]=\"Active == true\">{{ Name }}</span></div>", model);

        Assert.Contains("keep", content);
        Assert.DoesNotContain("drop", content);
    }

    [Fact]
    public void Nested_AtFor_Control_Flow()
    {
        var model = new { Rows = new[] { new { Cells = new[] { new { V = "x" }, new { V = "y" } } } } };

        var content = Render("<div>@for (Rows) { <span>@for (Cells) { <i>{{ V }}</i> }</span> }</div>", model);

        Assert.Contains("<i>x</i>", content);
        Assert.Contains("<i>y</i>", content);
    }

    // In a <table>, the @for/@if control-flow syntax cannot be used: its <ng-container> wrapper is
    // foster-parented out of the table by the HTML5 parser. Loop table rows/cells with the [for]
    // ATTRIBUTE on a real table element instead — that survives parsing.
    [Fact]
    public void For_Attribute_Works_Inside_A_Table()
    {
        var model = new { Rows = new[] { new { Cells = new[] { new { V = "x" }, new { V = "y" } } } } };

        var content = Render("<table><tr [for]=\"Rows\"><td [for]=\"Cells\">{{ V }}</td></tr></table>", model);

        Assert.Contains("<td>x</td>", content);
        Assert.Contains("<td>y</td>", content);
    }

    private static string Render(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
