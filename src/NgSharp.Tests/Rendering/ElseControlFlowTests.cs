
using NgSharp;

namespace NgSharp.Tests.Rendering;

public class ElseControlFlowTests
{
    // Rendering never minifies; normalize so assertions are whitespace-insensitive.
    private static string Render(string tpl, object model)
        => TestHtml.Minify(HtmlBuilder.Create().BuildFromTemplate(tpl, model));

    [Fact]
    public void If_Else_Takes_Then_When_True()
    {
        var content = Render("<div>@if (Ok == true) { <p>yes</p> } @else { <p>no</p> }</div>", new { Ok = true });

        Assert.Contains("<p>yes</p>", content);
        Assert.DoesNotContain("<p>no</p>", content);
    }

    [Fact]
    public void If_Else_Takes_Else_When_False()
    {
        var content = Render("<div>@if (Ok == true) { <p>yes</p> } @else { <p>no</p> }</div>", new { Ok = false });

        Assert.Contains("<p>no</p>", content);
        Assert.DoesNotContain("<p>yes</p>", content);
    }

    [Fact]
    public void If_ElseIf_Else_First_Branch()
    {
        var content = Render(
            "<div>@if (A == 1) { <p>a</p> } @else if (B == 1) { <p>b</p> } @else { <p>c</p> }</div>", new { A = 1, B = 1 });

        Assert.Contains("<p>a</p>", content);
        Assert.DoesNotContain("<p>b</p>", content);
        Assert.DoesNotContain("<p>c</p>", content);
    }

    [Fact]
    public void If_ElseIf_Else_Middle_Branch()
    {
        var content = Render(
            "<div>@if (A == 1) { <p>a</p> } @else if (B == 1) { <p>b</p> } @else { <p>c</p> }</div>", new { A = 0, B = 1 });

        Assert.Contains("<p>b</p>", content);
        Assert.DoesNotContain("<p>a</p>", content);
        Assert.DoesNotContain("<p>c</p>", content);
    }

    [Fact]
    public void If_ElseIf_Else_Fallthrough_To_Else()
    {
        var content = Render(
            "<div>@if (A == 1) { <p>a</p> } @else if (B == 1) { <p>b</p> } @else { <p>c</p> }</div>", new { A = 0, B = 0 });

        Assert.Contains("<p>c</p>", content);
        Assert.DoesNotContain("<p>a</p>", content);
        Assert.DoesNotContain("<p>b</p>", content);
    }

    [Fact]
    public void If_ElseIf_Without_Final_Else()
    {
        var none = Render("<div>@if (A == 1) { <p>a</p> } @else if (B == 1) { <p>b</p> }</div>", new { A = 0, B = 0 });
        Assert.DoesNotContain("<p>a</p>", none);
        Assert.DoesNotContain("<p>b</p>", none);

        var second = Render("<div>@if (A == 1) { <p>a</p> } @else if (B == 1) { <p>b</p> }</div>", new { A = 0, B = 1 });
        Assert.Contains("<p>b</p>", second);
    }

    [Fact]
    public void Plain_If_Without_Else_Still_Works()
    {
        var kept = Render("<div>@if (Ok == true) { <p>x</p> }</div>", new { Ok = true });
        Assert.Contains("<p>x</p>", kept);

        var dropped = Render("<div>@if (Ok == true) { <p>x</p> }</div>", new { Ok = false });
        Assert.DoesNotContain("<p>x</p>", dropped);
    }

    [Fact]
    public void Long_ElseIf_Chain_Selects_Third_Branch()
    {
        var content = Render(
            "<div>@if (N == 1) { <p>one</p> } @else if (N == 2) { <p>two</p> } @else if (N == 3) { <p>three</p> } @else { <p>many</p> }</div>",
            new { N = 3 });

        Assert.Contains("<p>three</p>", content);
        Assert.DoesNotContain("<p>one</p>", content);
        Assert.DoesNotContain("<p>two</p>", content);
        Assert.DoesNotContain("<p>many</p>", content);
    }

    [Fact]
    public void Nested_If_Else_Inside_Else_Branch()
    {
        var content = Render(
            "<div>@if (A == 1) { <p>a</p> } @else { @if (B == 1) { <p>b</p> } @else { <p>c</p> } }</div>",
            new { A = 0, B = 0 });

        Assert.Contains("<p>c</p>", content);
        Assert.DoesNotContain("<p>a</p>", content);
        Assert.DoesNotContain("<p>b</p>", content);
    }

    [Fact]
    public void If_Followed_By_Normal_Sibling_Is_Not_Consumed()
    {
        var content = Render("<div>@if (Ok == true) { <span>x</span> } <p>after</p></div>", new { Ok = false });

        Assert.DoesNotContain("<span>x</span>", content);
        Assert.Contains("<p>after</p>", content);
    }

    [Fact]
    public void If_Else_With_Logical_And_Condition()
    {
        var yes = Render("<div>@if (A == true && B == true) { <p>both</p> } @else { <p>not</p> }</div>", new { A = true, B = true });
        Assert.Contains("<p>both</p>", yes);

        var no = Render("<div>@if (A == true && B == true) { <p>both</p> } @else { <p>not</p> }</div>", new { A = true, B = false });
        Assert.Contains("<p>not</p>", no);
    }

    [Fact]
    public void Else_Branch_Renders_Interpolation()
    {
        var content = Render("<div>@if (Ok == true) { {{ Yes }} } @else { {{ No }} }</div>", new { Ok = false, Yes = "Y", No = "N" });

        Assert.Contains("N", content);
        Assert.DoesNotContain("Y", content);
    }

    [Fact]
    public void Comment_Between_Brace_And_Else_Does_Not_Break_Chain()
    {
        var truthy = Render("<div>@if (A == 1) { <p>a</p> } <!-- fallback --> @else { <p>b</p> }</div>", new { A = 1 });
        Assert.Contains("<p>a</p>", truthy);
        Assert.DoesNotContain("<p>b</p>", truthy);
        Assert.DoesNotContain("@else", truthy);

        var falsy = Render("<div>@if (A == 1) { <p>a</p> } <!-- fallback --> @else { <p>b</p> }</div>", new { A = 0 });
        Assert.Contains("<p>b</p>", falsy);
        Assert.DoesNotContain("<p>a</p>", falsy);
        Assert.DoesNotContain("@else", falsy);
    }

    [Fact]
    public void If_Else_Inside_For_Loop()
    {
        var content = Render(
            "<ul>@for (Items) { <li>@if (Ok == true) { <b>on</b> } @else { <b>off</b> }</li> }</ul>",
            new { Items = new[] { new { Ok = true }, new { Ok = false } } });

        Assert.Contains("<li><b>on</b></li>", content);
        Assert.Contains("<li><b>off</b></li>", content);
    }
}
