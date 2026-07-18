using NgSharp.Parsing;

namespace NgSharp.Tests.Parsing;

// Phase 1 of the AngleSharp removal: a purpose-built HTML template tokenizer.
// It tokenizes syntax only (tags/attrs/text/comments) and handles rawtext (<script>/<style>);
// void-ness and tree structure are the tree-builder's job. Interpolation {{ }} stays inside text.
public class HtmlLexerTests
{
    [Fact]
    public void Plain_text_is_one_text_token()
    {
        var token = Assert.Single(HtmlLexer.Tokenize("hello world"));

        Assert.Equal(HtmlTokenKind.Text, token.Kind);
        Assert.Equal("hello world", token.Value);
    }

    [Fact]
    public void Simple_element_open_text_close()
    {
        var tokens = HtmlLexer.Tokenize("<p>hi</p>");

        Assert.Equal(3, tokens.Count);
        Assert.Equal(HtmlTokenKind.OpenTag, tokens[0].Kind);
        Assert.Equal("p", tokens[0].Value);
        Assert.Equal(HtmlTokenKind.Text, tokens[1].Kind);
        Assert.Equal("hi", tokens[1].Value);
        Assert.Equal(HtmlTokenKind.CloseTag, tokens[2].Kind);
        Assert.Equal("p", tokens[2].Value);
    }

    [Fact]
    public void Text_before_and_after_element()
    {
        var tokens = HtmlLexer.Tokenize("a<b>c</b>d");

        Assert.Equal(5, tokens.Count);
        Assert.Equal("a", tokens[0].Value);
        Assert.Equal("c", tokens[2].Value);
        Assert.Equal("d", tokens[4].Value);
    }

    [Fact]
    public void Quoted_attributes()
    {
        var open = HtmlLexer.Tokenize("<a href=\"/x\" class=\"c\">")[0];

        Assert.Equal(2, open.Attributes.Count);
        Assert.Equal("href", open.Attributes[0].Name);
        Assert.Equal("/x", open.Attributes[0].Value);
        Assert.Equal("class", open.Attributes[1].Name);
        Assert.Equal("c", open.Attributes[1].Value);
    }

    [Fact]
    public void Boolean_unquoted_and_single_quoted_attributes()
    {
        var open = HtmlLexer.Tokenize("<input disabled data-x=unq title='s'>")[0];

        Assert.Equal(3, open.Attributes.Count);
        Assert.Equal("disabled", open.Attributes[0].Name);
        Assert.Equal("", open.Attributes[0].Value);
        Assert.Equal("data-x", open.Attributes[1].Name);
        Assert.Equal("unq", open.Attributes[1].Value);
        Assert.Equal("title", open.Attributes[2].Name);
        Assert.Equal("s", open.Attributes[2].Value);
    }

    [Fact]
    public void Bracketed_directive_attribute_names_are_preserved()
    {
        var open = HtmlLexer.Tokenize("<div [if]=\"A == 1\" [attr.class]=\"c\">")[0];

        Assert.Equal("[if]", open.Attributes[0].Name);
        Assert.Equal("A == 1", open.Attributes[0].Value);
        Assert.Equal("[attr.class]", open.Attributes[1].Name);
        Assert.Equal("c", open.Attributes[1].Value);
    }

    [Fact]
    public void Self_closing_tag_sets_flag()
    {
        var token = Assert.Single(HtmlLexer.Tokenize("<div/>"));

        Assert.Equal(HtmlTokenKind.OpenTag, token.Kind);
        Assert.True(token.SelfClosing);
        Assert.Equal("div", token.Value);
    }

    [Fact]
    public void Void_like_tag_without_slash_is_a_plain_open_tag()
    {
        var token = Assert.Single(HtmlLexer.Tokenize("<br>"));

        Assert.Equal(HtmlTokenKind.OpenTag, token.Kind);
        Assert.False(token.SelfClosing);
        Assert.Equal("br", token.Value);
    }

    [Fact]
    public void Comment_captures_inner_text()
    {
        var token = Assert.Single(HtmlLexer.Tokenize("<!-- hi -->"));

        Assert.Equal(HtmlTokenKind.Comment, token.Kind);
        Assert.Equal(" hi ", token.Value);
    }

    [Fact]
    public void Script_is_rawtext_inner_markup_not_parsed()
    {
        var tokens = HtmlLexer.Tokenize("<script>if (a < b && c > d) { go(); }</script>");

        Assert.Equal(3, tokens.Count);
        Assert.Equal("script", tokens[0].Value);
        Assert.Equal(HtmlTokenKind.Text, tokens[1].Kind);
        Assert.Equal("if (a < b && c > d) { go(); }", tokens[1].Value);
        Assert.Equal(HtmlTokenKind.CloseTag, tokens[2].Kind);
        Assert.Equal("script", tokens[2].Value);
    }

    [Fact]
    public void Style_is_rawtext_too()
    {
        var tokens = HtmlLexer.Tokenize("<style>.a { color: red; }</style>");

        Assert.Equal(".a { color: red; }", tokens[1].Value);
    }

    [Fact]
    public void Interpolation_stays_inside_text()
    {
        var tokens = HtmlLexer.Tokenize("<p>{{ User.Name }}</p>");

        Assert.Equal("{{ User.Name }}", tokens[1].Value);
    }

    [Fact]
    public void Doctype_declaration_is_dropped()
    {
        var tokens = HtmlLexer.Tokenize("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0//EN\" \"x.dtd\"><p>x</p>");

        Assert.Equal(3, tokens.Count);
        Assert.Equal(HtmlTokenKind.OpenTag, tokens[0].Kind);
        Assert.Equal("p", tokens[0].Value);
    }
}
