using System.Threading.Tasks;

using NgSharp;
using NgSharp.Tests.CustomElements;

namespace NgSharp.Tests.Extensibility;

// Custom directives (bridged, mutate a fresh host element) and [html] must work per-iteration in
// [for], including nested loops and outer-scope values; plus deep [for]/[if] combinations.
public class DirectivesAndDeepNestingTests
{
    private static Task<string> Render(string tpl, object model)
    {
        var builder = HtmlBuilder.Default;
        builder.RegisterDirective<HiddenDirective>();   // [hidden]="bool" -> sets the hidden attribute
        return builder.BuildFromTemplateAsync(tpl, model);
    }

    [Fact]
    public async Task Custom_Directive_Per_Iteration_In_Loop()
    {
        var model = new { Items = new[] { new { Hide = true, Name = "a" }, new { Hide = false, Name = "b" } } };

        var content = await Render("<ul><li [for]=\"Items\" [hidden]=\"Hide\">{{ Name }}</li></ul>", model);

        Assert.Contains("hidden", content);          // 'a' got the attribute
        Assert.Contains(">a</li>", content);
        Assert.Contains("<li>b</li>", content);       // 'b' did not
    }

    [Fact]
    public async Task Custom_Directive_In_Nested_Loop()
    {
        var model = new { Groups = new[] { new { Items = new[] { new { H = true, V = "x" }, new { H = false, V = "y" } } } } };

        var content = await Render("<div [for]=\"Groups\"><span [for]=\"Items\" [hidden]=\"H\">{{ V }}</span></div>", model);

        Assert.Contains("hidden", content);
        Assert.Contains("<span>y</span>", content);
    }

    [Fact]
    public async Task Custom_Directive_On_Outer_Scope_Value_From_Inner_Loop()
    {
        var model = new { Rows = new[] { new { Hide = true, Cells = new[] { new { V = "x" }, new { V = "y" } } } } };

        // [hidden] binds the OUTER row flag from inside the inner loop.
        var content = await Render("<div [for]=\"Rows\"><span [for]=\"Cells\" [hidden]=\"Hide\">{{ V }}</span></div>", model);

        Assert.DoesNotContain("<span>x</span>", content);   // both hidden
        Assert.Contains("hidden", content);
    }

    [Fact]
    public async Task Html_Binding_Per_Iteration_In_Loop()
    {
        var model = new { Items = new[] { new { Content = "<b>x</b>" }, new { Content = "<i>y</i>" } } };

        var content = await Render("<ul><li [for]=\"Items\" [html]=\"Content\"></li></ul>", model);

        Assert.Contains("<b>x</b>", content);
        Assert.Contains("<i>y</i>", content);
    }

    [Fact]
    public async Task Deep_For_If_For_With_Pipe()
    {
        var model = new
        {
            Groups = new[]
            {
                new { Active = true, Items = new[] { new { Name = "alice" }, new { Name = "bob" } } },
                new { Active = false, Items = new[] { new { Name = "carol" } } }
            }
        };

        var content = await Render(
            "<div [for]=\"Groups\"><div [if]=\"Active == true\"><span [for]=\"Items\">{{ Name | upper }}</span></div></div>", model);

        Assert.Contains("<span>ALICE</span>", content);
        Assert.Contains("<span>BOB</span>", content);
        Assert.DoesNotContain("CAROL", content);   // group 2 inactive
    }

    [Fact]
    public async Task Empty_Inner_Collection_Renders_Outer_Only()
    {
        var model = new { Groups = new[] { new { Title = "A", Items = new object[0] }, new { Title = "B", Items = new object[] { new { V = "z" } } } } };

        var content = await Render(
            "<div [for]=\"Groups\"><h2>{{ Title }}</h2><span [for]=\"Items\">{{ V }}</span></div>", model);

        Assert.Contains("<h2>A</h2>", content);
        Assert.Contains("<h2>B</h2>", content);
        Assert.Contains("<span>z</span>", content);
    }

    [Fact]
    public async Task For_Combined_With_NotEmpty()
    {
        var model = new
        {
            Groups = new[]
            {
                new { Items = new object[] { new { V = "x" } } },
                new { Items = new object[0] }
            }
        };

        // Each group's <ul> only renders when its Items is non-empty.
        var content = await Render(
            "<div [for]=\"Groups\"><ul [not-empty]=\"Items\"><li [for]=\"Items\">{{ V }}</li></ul></div>", model);

        Assert.Contains("<li>x</li>", content);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(content, "<ul>"));   // only the non-empty group has a <ul>
    }
}
