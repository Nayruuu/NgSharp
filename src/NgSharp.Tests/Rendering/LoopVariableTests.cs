
using NgSharp;

namespace NgSharp.Tests.Rendering;

// The implicit loop variables inside [for]/@for: $index (0-based), $count, $first, $last — resolved
// against the NEAREST enclosing loop frame, before any scope-chain walk. Outside a loop they are null
// (render empty / falsy), exactly like Angular's @for contextual variables.
public class LoopVariableTests
{
    [Fact]
    public void Index_And_Count_Number_The_Items()
        => Assert.Equal("<li>0/3 A</li><li>1/3 B</li><li>2/3 C</li>",
            Render("<li [for]=\"Items\">{{ $index }}/{{ $count }} {{ Name }}</li>",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" }, new { Name = "C" } } }));

    [Fact]
    public void Index_Plus_One_Gives_Invoice_Line_Numbers()
        => Assert.Equal("<tr><td>1</td></tr><tr><td>2</td></tr>",
            Render("<tr [for]=\"Lines\"><td>{{ $index + 1 }}</td></tr>",
                new { Lines = new[] { new { Id = 7 }, new { Id = 8 } } }));

    [Fact]
    public void First_And_Last_Flag_The_Boundary_Items()
        => Assert.Equal("<i class=\"first\">A</i><i>B</i><i class=\"last\">C</i>",
            Render("<i [for]=\"Items\" [class.first]=\"$first\" [class.last]=\"$last\">{{ Name }}</i>",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" }, new { Name = "C" } } }));

    [Fact]
    public void Single_Item_Is_Both_First_And_Last()
        => Assert.Equal("<b>True/True</b>",
            Render("<b [for]=\"Items\">{{ $first }}/{{ $last }}</b>",
                new { Items = new[] { new { Name = "A" } } }));

    [Fact]
    public void Nested_Loops_Resolve_The_Inner_Frame()
        => Assert.Equal("<p>0.0</p><p>0.1</p><p>1.0</p>",
            Render("@for (g of Groups) {@for (v of g.Values) {<p>{{ g.I }}.{{ $index }}</p>}}",
                new
                {
                    Groups = new[]
                    {
                        new { I = 0, Values = new[] { 1, 2 } },
                        new { I = 1, Values = new[] { 3 } },
                    },
                }));

    [Fact]
    public void Named_For_Frame_Still_Exposes_Its_Position()
        => Assert.Equal("<li>0:A</li><li>1:B</li>",
            Render("@for (p of Items) {<li>{{ $index }}:{{ p.Name }}</li>}",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" } } }));

    [Fact]
    public void Outside_A_Loop_They_Are_Null()
        => Assert.Equal("<p></p><span>out</span>",
            Render("<p>{{ $index }}{{ $count }}</p><span [if]=\"!$first\">out</span>",
                new { Anything = 1 }));

    [Fact]
    public void Unknown_Dollar_Name_Is_Null_Even_Inside_A_Loop()
        => Assert.Equal("<li></li>",
            Render("<li [for]=\"Items\">{{ $middle }}</li>",
                new { Items = new[] { new { Name = "A" } } }));

    [Fact]
    public void Text_Mode_Blocks_See_The_Loop_Variables()
        => Assert.Equal("1/2:A\n2/2:B\n",
            HtmlBuilder.Create().BuildFromTemplate(
                "@for (Items) {{{ $index + 1 }}/{{ $count }}:{{ Name }}\n}",
                new { Items = new[] { new { Name = "A" }, new { Name = "B" } } },
                new TemplateOptions { Mode = TemplateMode.Text }));

    private static string Render(string tpl, object model) => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
