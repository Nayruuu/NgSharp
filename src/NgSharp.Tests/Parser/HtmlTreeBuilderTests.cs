using NgSharp.Template;

namespace NgSharp.Tests.Parser;

// Phase 2 of the AngleSharp removal: tokens -> lightweight HtmlNode tree. Void-aware, lenient on
// mismatched/unclosed tags (no HTML5 error-recovery), rawtext content becomes a text child.
public class HtmlTreeBuilderTests
{
    private static IReadOnlyList<HtmlNode> Build(string html) => HtmlTreeBuilder.Build(HtmlLexer.Tokenize(html));

    [Fact]
    public void Single_element()
    {
        var div = Assert.Single(Build("<div></div>"));

        Assert.Equal(HtmlNodeType.Element, div.NodeType);
        Assert.Equal("div", div.Name);
        Assert.Empty(div.Children);
    }

    [Fact]
    public void Element_with_text_child()
    {
        var div = Assert.Single(Build("<div>hi</div>"));
        var text = Assert.Single(div.Children);

        Assert.Equal(HtmlNodeType.Text, text.NodeType);
        Assert.Equal("hi", text.Text);
    }

    [Fact]
    public void Nested_elements()
    {
        var div = Assert.Single(Build("<div><span>x</span></div>"));
        var span = Assert.Single(div.Children);

        Assert.Equal("span", span.Name);
        Assert.Equal("x", Assert.Single(span.Children).Text);
    }

    [Fact]
    public void Sibling_roots()
    {
        var roots = Build("<a></a><b></b>");

        Assert.Equal(2, roots.Count);
        Assert.Equal("a", roots[0].Name);
        Assert.Equal("b", roots[1].Name);
    }

    [Fact]
    public void Void_element_is_a_leaf_not_a_parent()
    {
        var div = Assert.Single(Build("<div><br>after</div>"));

        Assert.Equal(2, div.Children.Count);
        Assert.Equal("br", div.Children[0].Name);
        Assert.Empty(div.Children[0].Children);
        Assert.Equal("after", div.Children[1].Text);
    }

    [Fact]
    public void Self_closing_is_a_leaf()
    {
        var div = Assert.Single(Build("<div><x/>after</div>"));

        Assert.Equal(2, div.Children.Count);
        Assert.Equal("x", div.Children[0].Name);
        Assert.Empty(div.Children[0].Children);
        Assert.Equal("after", div.Children[1].Text);
    }

    [Fact]
    public void Attributes_are_carried_onto_the_node()
    {
        var div = Assert.Single(Build("<div [if]=\"A == 1\" class=\"c\"></div>"));

        Assert.Equal("A == 1", div.GetAttribute("[if]"));
        Assert.Equal("c", div.GetAttribute("class"));
        Assert.True(div.HasAttribute("[if]"));
        Assert.Null(div.GetAttribute("missing"));
        Assert.False(div.HasAttribute("missing"));
    }

    [Fact]
    public void Comment_node()
    {
        var comment = Assert.Single(Build("<!-- hi -->"));

        Assert.Equal(HtmlNodeType.Comment, comment.NodeType);
        Assert.Equal(" hi ", comment.Text);
    }

    [Fact]
    public void Script_rawtext_becomes_a_text_child()
    {
        var script = Assert.Single(Build("<script>a<b && c</script>"));

        Assert.Equal("script", script.Name);
        Assert.Equal("a<b && c", Assert.Single(script.Children).Text);
    }

    [Fact]
    public void Unclosed_element_is_auto_closed_at_eof()
    {
        var div = Assert.Single(Build("<div>hi"));

        Assert.Equal("div", div.Name);
        Assert.Equal("hi", Assert.Single(div.Children).Text);
    }

    [Fact]
    public void Stray_close_tag_is_ignored()
    {
        var text = Assert.Single(Build("</div>text"));

        Assert.Equal(HtmlNodeType.Text, text.NodeType);
        Assert.Equal("text", text.Text);
    }

    [Fact]
    public void Mismatched_close_implicitly_closes_inner_element()
    {
        var b = Assert.Single(Build("<b><i>x</b>"));

        Assert.Equal("b", b.Name);
        var i = Assert.Single(b.Children);
        Assert.Equal("i", i.Name);
        Assert.Equal("x", Assert.Single(i.Children).Text);
    }

    [Fact]
    public void Full_document_tree()
    {
        var html = Assert.Single(Build("<html><head><title>t</title></head><body><p>x</p></body></html>"));

        Assert.Equal("html", html.Name);
        Assert.Equal(2, html.Children.Count);
        Assert.Equal("head", html.Children[0].Name);
        Assert.Equal("body", html.Children[1].Name);
        Assert.Equal("p", Assert.Single(html.Children[1].Children).Name);
    }
}
