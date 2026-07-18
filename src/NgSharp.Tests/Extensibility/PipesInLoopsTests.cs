using System;
using System.Threading.Tasks;

using NgSharp;

namespace NgSharp.Tests.Extensibility;

// Pipes and attribute/class/style bindings must work per-iteration inside [for] loops — including
// nested loops and outer-scope fields — since the renderer builds a fresh host element per element.
public class PipesInLoopsTests
{
    private static Task<string> Render(string tpl, object model)
        => HtmlBuilder.Default.BuildFromTemplateAsync(tpl, model);

    [Fact]
    public async Task Pipe_In_For_Loop()
    {
        var model = new { Items = new[] { new { Name = "alice" }, new { Name = "bob" } } };

        var content = await Render("<ul><li [for]=\"Items\">{{ Name | upper }}</li></ul>", model);

        Assert.Contains("<li>ALICE</li>", content);
        Assert.Contains("<li>BOB</li>", content);
    }

    [Fact]
    public async Task Pipe_With_Argument_In_Loop()
    {
        var model = new { Events = new[] { new { D = new DateTime(2021, 1, 1) }, new { D = new DateTime(2022, 1, 1) } } };

        var content = await Render("<ul><li [for]=\"Events\">{{ D | date: 'yyyy' }}</li></ul>", model);

        Assert.Contains("<li>2021</li>", content);
        Assert.Contains("<li>2022</li>", content);
    }

    [Fact]
    public async Task Number_Pipe_In_Loop()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var model = new { Rows = new[] { new { N = 1000 }, new { N = 2500 } } };

        var content = await Render("<ul><li [for]=\"Rows\">{{ N | number: 'N0' }}</li></ul>", model);

        Assert.Contains("<li>1,000</li>", content);
        Assert.Contains("<li>2,500</li>", content);
    }

    [Fact]
    public async Task Pipe_In_Nested_Loop_On_Inner_Field()
    {
        var model = new { Groups = new[] { new { Items = new[] { new { Name = "x" }, new { Name = "y" } } } } };

        var content = await Render("<div [for]=\"Groups\"><span [for]=\"Items\">{{ Name | upper }}</span></div>", model);

        Assert.Contains("<span>X</span>", content);
        Assert.Contains("<span>Y</span>", content);
    }

    [Fact]
    public async Task Pipe_In_Nested_Loop_On_Outer_Scope_Field()
    {
        var model = new { Cats = new[] { new { Cat = "fruit", Items = new[] { new { P = "apple" }, new { P = "pear" } } } } };

        // {{ Cat | upper }} is an OUTER field piped from inside the inner loop.
        var content = await Render("<div [for]=\"Cats\"><span [for]=\"Items\">{{ Cat | upper }}:{{ P }};</span></div>", model);

        Assert.Contains("FRUIT:apple;", content);
        Assert.Contains("FRUIT:pear;", content);
    }

    [Fact]
    public async Task Attr_Binding_Per_Iteration_In_Loop()
    {
        var model = new { Items = new[] { new { Css = "red", Name = "x" }, new { Css = "blue", Name = "y" } } };

        var content = await Render("<ul><li [for]=\"Items\" [attr.class]=\"Css\">{{ Name }}</li></ul>", model);

        Assert.Contains("<li class=\"red\">x</li>", content);
        Assert.Contains("<li class=\"blue\">y</li>", content);
    }

    [Fact]
    public async Task Class_Toggle_Per_Iteration_In_Loop()
    {
        var model = new { Items = new[] { new { On = true, Name = "a" }, new { On = false, Name = "b" } } };

        var content = await Render("<ul><li [for]=\"Items\" [class.active]=\"On == true\">{{ Name }}</li></ul>", model);

        Assert.Contains("class=\"active\">a", content);
        Assert.DoesNotContain("class=\"active\">b", content);
    }

    [Fact]
    public async Task Style_Binding_Per_Iteration_In_Loop()
    {
        var model = new { Items = new[] { new { W = "bold", Name = "a" }, new { W = "normal", Name = "b" } } };

        var content = await Render("<ul><li [for]=\"Items\" [style.font-weight]=\"W\">{{ Name }}</li></ul>", model);

        Assert.Contains("font-weight: bold", content);
        Assert.Contains("font-weight: normal", content);
    }

    [Fact]
    public async Task Binding_On_Outer_Field_From_Inner_Loop()
    {
        var model = new { Groups = new[] { new { Css = "g1", Items = new[] { new { Name = "x" } } } } };

        var content = await Render("<div [for]=\"Groups\"><span [for]=\"Items\" [attr.class]=\"Css\">{{ Name }}</span></div>", model);

        Assert.Contains("<span class=\"g1\">x</span>", content);
    }
}
