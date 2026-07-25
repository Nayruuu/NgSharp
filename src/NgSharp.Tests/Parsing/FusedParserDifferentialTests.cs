
using NgSharp;
using NgSharp.Parsing;
using NgSharp.Rendering;

namespace NgSharp.Tests.Parsing;

// Differential oracle for the fused single-pass parser: for every gnarly construct, the FUSED parse
// (production path) and the STAGED pipeline (lexer -> tree-builder -> tree walk, kept as the reference
// implementation) must render byte-identical output.
public class FusedParserDifferentialTests
{
    private static readonly object Model = new
    {
        Name = "Bob & Co",
        Show = true,
        Hide = false,
        Items = new[] { new { V = 1 }, new { V = 2 } },
    };

    [Theory]
    // Plain structure, attributes (quoted/unquoted/valueless), self-closing and void elements.
    [InlineData("<div class=\"a\" data-x=1 disabled><br><img src=\"x.png\"/><hr/></div>")]
    // Interpolations, escaping, bare '<' as literal text.
    [InlineData("<p>a < b et {{ Name }} & fin</p>")]
    // Nested loops + conditionals + else chains (with interstitial whitespace + comments).
    [InlineData("@for (Items) {<li [if]=\"Show\">{{ V }}</li>}")]
    [InlineData("@if (Show) {<b>oui</b>} @else if (Hide) {<i>ei</i>} @else {<u>non</u>}")]
    [InlineData("<div [if]=\"Hide\">A</div>  <!-- x -->  <div [else]=\"\">B</div>")]
    // An [else] NOT preceded by an if renders plainly (marker stripped).
    [InlineData("<span>x</span><div [else]=\"\">B</div>")]
    // Rawtext: <script>/<style> content is literal (tags inside are NOT parsed), interpolation still works.
    [InlineData("<script>if (a<b && c>d) { x('<div>'); } var n = {{ Name }};</script>")]
    [InlineData("<style>.a>b { color: red; }</style>")]
    // Lenient recovery: implicit close (</div> closes the open <span>), stray close ignored.
    [InlineData("<div><span>txt</div>")]
    [InlineData("<div>a</p>b</div>")]
    // Declarations dropped; comments preserved; unclosed at EOF auto-closes.
    [InlineData("<!doctype html><div>a<!-- c -->b")]
    // ng-template + @render.
    [InlineData("<ng-template #frag><em>{{ Name }}</em></ng-template>@render(frag)")]
    // Case-insensitivity of tags/attributes; '#' attr case preserved.
    [InlineData("<DIV CLASS=\"x\"><SPAN>y</SPAN></DIV>")]
    // Structural on one element with everything.
    [InlineData("<ul [not-empty]=\"Items\"><li [for]=\"Items\" [class.on]=\"Show\" [attr.data-v]=\"V\">{{ V }}</li></ul>")]
    // [empty], the dual of [not-empty] — one branch dropped (Items has entries), one rendered (Missing is absent).
    [InlineData("<ul [empty]=\"Items\"><li>none</li></ul><p [empty]=\"Missing\">none</p>")]
    // {{- / -}} whitespace-control markers (incl. the {{- -}} eater); negation stays untouched.
    [InlineData("<p>a  {{- Name -}}  b {{ -3 }} c</p>")]
    [InlineData("<p>x \n {{- -}} \n y{{ Name -}}   {{- Name }}</p>")]
    // @switch / @case / @default: block form, nested in @for, hand-written markers, stray content dropped.
    [InlineData("@switch (Show) { @case (true) {<b>on</b>} @case (false) {<i>off</i>} @default {<u>?</u>} }")]
    [InlineData("@for (Items) {<li>@switch (V) { @case (1) {<b>un</b>} @default {<i>autre</i>} }</li>}")]
    [InlineData("<ng-container [switch]=\"Show\"><ng-container [case]=\"false\">A</ng-container><ng-container [default]=\"\">B</ng-container></ng-container>")]
    [InlineData("@switch (Show) { <p>stray</p> {{ Name }} @case (true) {<b>on</b>} }")]
    public void Fused_And_Staged_Parsers_Render_Identically(string template) => AssertSameRender(template);

    [Fact]
    public void Fused_Parser_Serves_The_Public_Api()
    {
        var html = HtmlBuilder.Create().BuildFromTemplate("<p>{{ Name }}</p>", Model);
        Assert.Equal("<p>Bob &amp; Co</p>", html);
    }

    private static string RenderVia(System.Collections.Generic.IReadOnlyList<NgSharp.Ast.TemplateNode> nodes)
        => TemplateRenderer.Render(nodes, NgElement.FromObject(Model), HtmlBuilder.Create().Pipes, null, null, TemplateRenderer.CollectTemplates(nodes));

    private static void AssertSameRender(string template)
    {
        var fused = RenderVia(TemplateParser.ParseDocument(template));
        var staged = RenderVia(StagedTemplateParser.ParseRootsViaStagedPipeline(template));

        Assert.Equal(staged, fused);
    }
}
