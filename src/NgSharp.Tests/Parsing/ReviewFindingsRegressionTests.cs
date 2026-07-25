using System.Linq;

using NgSharp;
using NgSharp.Parsing;
using NgSharp.Rendering;

namespace NgSharp.Tests.Parsing;

// Each test locks a reproduced divergence the byte-identity corpus did not cover.
public class ReviewFindingsRegressionTests
{
    [Fact]
    public void SelfClosed_NonVoid_Element_Still_Emits_Its_Close_Tag()
        => Assert.Equal("<div><span></span>x</div>", Render("<div><span/>x</div>", new { }));

    [Fact]
    public void SelfClosed_NonVoid_With_Structural_Still_Closes()
        => Assert.Equal("<p></p>", Render("<p [if]=\"Show\"/>", new { Show = true }));

    [Fact]
    public void NonAscii_Uppercase_Attribute_Name_Is_Lowercased_Like_The_Staged_Path()
    {
        // 'Ä' must NOT take the verbatim span fast path (ToLowerInvariant would produce 'ä' on the full path).
        var fused = TemplateRenderer.Render(TemplateParser.ParseDocument("<div Ä=\"x\">y</div>"), NgElement.FromObject(new { }), null, null);
        var staged = TemplateRenderer.Render(StagedTemplateParser.ParseRootsViaStagedPipeline("<div Ä=\"x\">y</div>"), NgElement.FromObject(new { }), null, null);

        Assert.Equal(staged, fused);
    }

    [Fact]
    public void Rawtext_Content_After_A_Mismatched_Close_Is_Preserved_Raw()
        => Assert.Equal("<script>ab&c</script>", Render("<script>a</scriptx>b&c</script>", new { }));

    [Fact]
    public void Object_Compared_To_Itself_Is_Equal_On_The_Lazy_Object_Path()
        => Assert.Equal("<i>eq</i>", Render("<i [if]=\"A == A\">eq</i>", new { A = new { X = 1 } }));

    [Theory]
    [InlineData("{{ 007 }}", "007")]      // leading zero: not a JSON number -> string
    [InlineData("{{ 1. }}", "1.")]        // dangling dot -> string
    [InlineData("{{ 1.2.3 }}", "1.2.3")]  // two dots -> string
    [InlineData("{{ 0.5 }}", "0.5")]      // valid: renders the number
    [InlineData("{{ 0 }}", "0")]
    public void Numeric_Literals_Keep_The_Json_Grammar_Strictness(string tpl, string expected)
        => Assert.Equal($"<p>{expected}</p>", Render($"<p>{tpl}</p>", new { }));

    [Fact]
    public void Concurrent_First_Renders_Of_A_Shared_Compiled_Template_Never_Lose_The_Pipe()
    {
        // Stresses concurrent first-render pipe resolution (a two-field memo was reproducibly racy:
        // transient 'Unknown pipe'); the immutable-holder memo must survive it.
        for (var round = 0; round < 20; round++)
        {
            var compiled = HtmlBuilder.Create().Compile("<p>{{ Name | upper }}</p>");
            var results = Enumerable.Range(0, 8).AsParallel().WithDegreeOfParallelism(8)
                .Select(_ => compiled.Render(new { Name = "x" })).ToList();

            Assert.All(results, r => Assert.Equal("<p>X</p>", r));
        }
    }

    private static string Render(string tpl, object model) => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
