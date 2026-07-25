using NgSharp;

namespace NgSharp.Tests.Rendering;

// The [empty] structural directive — the dual of [not-empty]: the host element renders when the
// collection is empty or absent.
public class EmptyDirectiveTests
{
    [Fact]
    public void Empty_Renders_When_Collection_Is_Empty()
        => Assert.Equal("<div>none</div>", Render("<div [empty]=\"Items\">none</div>", new { Items = new int[0] }));

    [Fact]
    public void Empty_Removes_When_Collection_Has_Items()
        => Assert.Equal("", Render("<div [empty]=\"Items\">none</div>", new { Items = new[] { 1 } }));

    [Fact]
    public void Empty_Renders_When_Collection_Is_Absent()
        => Assert.Equal("<div>none</div>", Render("<div [empty]=\"Items\">none</div>", new { X = 1 }));

    [Fact]
    public void Empty_And_NotEmpty_Render_Exactly_One_Branch()
    {
        var template = "<ul [not-empty]=\"Items\"><li>full</li></ul><p [empty]=\"Items\">empty</p>";

        Assert.Equal("<ul><li>full</li></ul>", Render(template, new { Items = new[] { 1 } }));
        Assert.Equal("<p>empty</p>", Render(template, new { Items = new int[0] }));
    }

    [Fact]
    public void Empty_Nested_In_A_For_Reads_The_Item_Scope()
    {
        var template = "<div [for]=\"Groups\"><span [empty]=\"Members\">no members</span></div>";
        var model = new
        {
            Groups = new object[]
            {
                new { Members = new int[0] },
                new { Members = new[] { 1 } },
            }
        };

        Assert.Equal("<div><span>no members</span></div><div></div>", Render(template, model));
    }

    [Fact]
    public void Empty_Renders_When_A_String_Collection_Path_Is_Null()
        => Assert.Equal("<div>none</div>", Render("<div [empty]=\"Items\">none</div>", new { Items = (int[])null }));

    private static string Render(string template, object model)
        => HtmlBuilder.Create().BuildFromTemplate(template, model);
}
