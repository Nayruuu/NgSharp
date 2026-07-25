
using NgSharp;
using NgSharp.Components;

namespace NgSharp.Tests.Extensibility;

// Server components inside [for]: each iteration must get a FRESH instance with the current item's
// data (RenderComponent does Activator.CreateInstance per render), including nested loops and
// outer-scope bindings. CustomComponent renders <div>{ComponentText}</div>.
public class ComponentsInLoopsTests
{
    [Fact]
    public void Component_In_For_Loop_Renders_Per_Item()
    {
        var model = new { Items = new[] { new { Name = "A" }, new { Name = "B" } } };

        var content = Render(
            "<ul><li [for]=\"Items\"><custom-component [ComponentText]=\"Name\"></custom-component></li></ul>", model);

        Assert.Contains("<div>A</div>", content);
        Assert.Contains("<div>B</div>", content);
    }

    [Fact]
    public void Fresh_Component_Instance_Per_Iteration()
    {
        var model = new { Items = new[] { new { Name = "one" }, new { Name = "two" }, new { Name = "three" } } };

        var content = Render(
            "<div [for]=\"Items\"><custom-component [ComponentText]=\"Name\"></custom-component></div>", model);

        Assert.Contains("<div>one</div>", content);
        Assert.Contains("<div>two</div>", content);
        Assert.Contains("<div>three</div>", content);
    }

    [Fact]
    public void Component_In_Nested_Loop()
    {
        var model = new { Groups = new[] { new { Items = new[] { new { V = "x" }, new { V = "y" } } } } };

        var content = Render(
            "<div [for]=\"Groups\"><span [for]=\"Items\"><custom-component [ComponentText]=\"V\"></custom-component></span></div>", model);

        Assert.Contains("<div>x</div>", content);
        Assert.Contains("<div>y</div>", content);
    }

    [Fact]
    public void Component_Binding_On_Outer_Scope_Field_From_Inner_Loop()
    {
        var model = new { Cats = new[] { new { Cat = "fruit", Items = new[] { new { P = "apple" }, new { P = "pear" } } } } };

        var content = Render(
            "<div [for]=\"Cats\"><span [for]=\"Items\"><custom-component [ComponentText]=\"Cat\"></custom-component></span></div>", model);

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(content, "<div>fruit</div>").Count);
    }

    [Fact]
    public void Component_With_If_In_Loop()
    {
        var model = new { Items = new[] { new { Show = true, Name = "keep" }, new { Show = false, Name = "drop" } } };

        var content = Render(
            "<ul><li [for]=\"Items\"><custom-component [if]=\"Show == true\" [ComponentText]=\"Name\"></custom-component></li></ul>", model);

        Assert.Contains("keep", content);
        Assert.DoesNotContain("drop", content);
    }

    [Fact]
    public void For_Directly_On_Component_Element()
    {
        var model = new { Items = new[] { new { Name = "a" }, new { Name = "b" }, new { Name = "c" } } };

        var content = Render(
            "<div><custom-component [for]=\"Items\" [ComponentText]=\"Name\"></custom-component></div>", model);

        Assert.Contains("<div>a</div>", content);
        Assert.Contains("<div>b</div>", content);
        Assert.Contains("<div>c</div>", content);
    }

    [Fact]
    public void If_Directly_On_Component_Element()
    {
        var hidden = Render("<div><custom-component [if]=\"Ok == true\" [ComponentText]=\"Name\"></custom-component></div>",
            new { Ok = false, Name = "secret" });
        Assert.DoesNotContain("secret", hidden);

        var shown = Render("<div><custom-component [if]=\"Ok == true\" [ComponentText]=\"Name\"></custom-component></div>",
            new { Ok = true, Name = "visible" });
        Assert.Contains("<div>visible</div>", shown);
    }

    [Fact]
    public void NotEmpty_Directly_On_Component_Element()
    {
        var shown = Render("<div><custom-component [not-empty]=\"Items\" [ComponentText]=\"Title\"></custom-component></div>",
            new { Items = new[] { 1, 2 }, Title = "shown" });
        Assert.Contains("<div>shown</div>", shown);

        var hidden = Render("<div><custom-component [not-empty]=\"Items\" [ComponentText]=\"Title\"></custom-component></div>",
            new { Items = new int[0], Title = "hidden" });
        Assert.DoesNotContain("hidden", hidden);
    }

    [Fact]
    public void For_And_If_On_Same_Component_Element()
    {
        var model = new
        {
            Items = new[]
            {
                new { Show = true, Name = "a" },
                new { Show = false, Name = "b" },
                new { Show = true, Name = "c" }
            }
        };

        var content = Render(
            "<div><custom-component [for]=\"Items\" [if]=\"Show == true\" [ComponentText]=\"Name\"></custom-component></div>", model);

        Assert.Contains("<div>a</div>", content);
        Assert.Contains("<div>c</div>", content);
        Assert.DoesNotContain("<div>b</div>", content);
    }

    private static string Render(string tpl, object model)
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<CustomComponent>();

        return builder.BuildFromTemplate(tpl, model);
    }
}
