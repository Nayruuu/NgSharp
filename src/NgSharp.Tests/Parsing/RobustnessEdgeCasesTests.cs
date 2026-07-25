
using NgSharp;
using NgSharp.Tests.CustomElements;

namespace NgSharp.Tests.Parsing;

// The nastiest interactions: many directives/bindings on one element at once, escaping, and
// @if/@for control-flow nesting.
public class RobustnessEdgeCasesTests
{
    [Fact]
    public void Kitchen_Sink_For_If_Attr_Directive_Pipe_On_One_Element()
    {
        var model = new
        {
            Items = new[]
            {
                new { Show = true, Css = "on", H = false, Name = "alice" },
                new { Show = true, Css = "off", H = true, Name = "bob" },
                new { Show = false, Css = "x", H = false, Name = "carol" }
            }
        };

        var content = Render(
            "<ul><li [for]=\"Items\" [if]=\"Show == true\" [attr.class]=\"Css\" [hidden]=\"H\">{{ Name | upper }}</li></ul>", model);

        Assert.Contains("class=\"on\"", content);
        Assert.Contains("ALICE", content);
        Assert.Contains("class=\"off\"", content);
        Assert.Contains("hidden", content);        // bob is hidden
        Assert.Contains("BOB", content);
        Assert.DoesNotContain("CAROL", content);   // carol filtered out by [if]
    }

    [Fact]
    public void Interpolation_Escapes_Special_Chars_In_Loop()
    {
        var model = new { Items = new[] { new { X = "<b>&\"'</b>" }, new { X = "a & b" } } };

        var content = Render("<ul><li [for]=\"Items\">{{ X }}</li></ul>", model);

        Assert.DoesNotContain("<b>&\"'</b>", content);   // must be escaped, not injected
        Assert.Contains("&lt;b&gt;", content);
        Assert.Contains("a &amp; b", content);
    }

    [Fact]
    public void Unicode_And_Accents_In_Loop()
    {
        var model = new { Items = new[] { new { N = "café" }, new { N = "naïve €" }, new { N = "日本語" } } };

        var content = Render("<ul><li [for]=\"Items\">{{ N }}</li></ul>", model);

        Assert.Contains("café", content);
        Assert.Contains("naïve €", content);
        Assert.Contains("日本語", content);
    }

    [Fact]
    public void AtIf_Inside_AtFor()
    {
        var model = new { Items = new[] { new { Ok = true, N = "keep" }, new { Ok = false, N = "drop" } } };

        var content = Render("<div>@for (Items) { <span>@if (Ok == true) { {{ N }} }</span> }</div>", model);

        Assert.Contains("keep", content);
        Assert.DoesNotContain("drop", content);
    }

    [Fact]
    public void AtFor_Inside_AtIf()
    {
        var model = new { Enabled = true, Items = new[] { new { N = "x" }, new { N = "y" } } };

        var content = Render("<div>@if (Enabled == true) { <ul>@for (Items) { <li>{{ N }}</li> }</ul> }</div>", model);

        Assert.Contains("<li>x</li>", content);
        Assert.Contains("<li>y</li>", content);
    }

    [Fact]
    public void Five_Level_Deep_Nesting()
    {
        var model = new { A = new[] { new { B = new[] { new { C = new[] { new { D = new[] { new { E = new[] { new { V = "deep" } } } } } } } } } } };

        var content = Render(
            "<div [for]=\"A\"><div [for]=\"B\"><div [for]=\"C\"><div [for]=\"D\"><span [for]=\"E\">{{ V }}</span></div></div></div></div>", model);

        Assert.Contains("<span>deep</span>", content);
    }

    [Fact]
    public void Empty_Collection_Renders_Nothing_No_Crash()
    {
        var content = Render("<ul><li [for]=\"Items\">{{ N }}</li></ul>", new { Items = new object[0] });

        Assert.Contains("<ul></ul>", content);
    }

    private static string Render(string tpl, object model)
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterDirective<HiddenDirective>();

        return builder.BuildFromTemplate(tpl, model);
    }
}
