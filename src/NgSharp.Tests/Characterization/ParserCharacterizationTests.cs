using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

using NgSharp;

namespace NgSharp.Tests.Characterization;

// Safety net for the AngleSharp removal (docs/remove-anglesharp-plan.md, Phase 0).
//
// Each case renders through the CURRENT engine and is locked against a committed golden file.
// Missing golden -> captured on first run (then commit it). Present golden -> asserted equal.
// The goldens encode current behaviour verbatim, INCLUDING current warts (e.g. MinifyHtml
// collapsing whitespace inside <pre>/<script>) — the parser swap must preserve them byte-for-byte.
// When a wart is intentionally fixed later, regenerate only that case's golden in the same commit.
public class ParserCharacterizationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return Case("interpolation", "<p>{{ User.Name }} — {{ User.City }}</p>",
            @"{""User"":{""Name"":""Ada"",""City"":""Paris""}}");

        yield return Case("pipes", "<p>{{ Title | upper }} · {{ D | date:'dd/MM/yyyy' }} · {{ N | number }}</p>",
            @"{""Title"":""hello"",""D"":""2026-07-17T00:00:00"",""N"":1234.5}");

        yield return Case("if-operators",
            "<div><span [if]=\"A == 1 && B == 2\">and</span><span [if]=\"A == 9 || B == 2\">or</span><span [if]=\"X != null\">notnull</span></div>",
            @"{""A"":1,""B"":2,""X"":""v""}");

        yield return Case("for-nested",
            "<ul>{{ '' }}<li [for]=\"Groups\">{{ Name }}<span [for]=\"Items\">{{ Label }}</span></li></ul>",
            @"{""Groups"":[{""Name"":""G1"",""Items"":[{""Label"":""a""},{""Label"":""b""}]},{""Name"":""G2"",""Items"":[]}]}");

        yield return Case("not-empty",
            "<div><section [not-empty]=\"Rows\"><b [for]=\"Rows\">{{ V }}</b></section><section [not-empty]=\"Empty\">hidden</section></div>",
            @"{""Rows"":[{""V"":1},{""V"":2}],""Empty"":[]}");

        yield return Case("bindings-merge",
            "<div [attr.class]=\"Extra\" class=\"base\"><a [attr.href]=\"Link\">x</a><i [style.font-weight]=\"W\">y</i><u [class.on]=\"Flag\">z</u></div>",
            @"{""Extra"":""added"",""Link"":""/go"",""W"":""bold"",""Flag"":true}");

        yield return Case("html-binding", "<div [html]=\"Raw\"></div>",
            @"{""Raw"":""<b>bold</b> & <i>it</i>""}");

        yield return Case("at-if-else",
            "<div>@if (S == 1) { <p>one</p> } @else if (S == 2) { <p>two</p> } @else { <p>other</p> }</div>",
            @"{""S"":2}");

        yield return Case("at-for", "<ul>@for (Items) { <li>{{ Name }}</li> }</ul>",
            @"{""Items"":[{""Name"":""x""},{""Name"":""y""}]}");

        yield return Case("comments", "<div><!-- lead -->text<!-- trail --></div>", "{}");

        yield return Case("void-elements",
            "<div><br><img src=\"/a.png\" alt=\"a\"><input type=\"text\"><hr><meta charset=\"utf-8\"></div>", "{}");

        yield return Case("rawtext-script-style",
            "<div><style>.a { color: red; }</style><script>if (a < b && c > d) { go(); }</script></div>", "{}");

        yield return Case("attributes-edge",
            "<div><input disabled required><span data-x=unquoted class='single'>t</span></div>", "{}");

        yield return Case("entities", "<p>caf&eacute; &amp; TVA &lt; 20% &gt; 0</p>", "{}");

        yield return Case("full-document",
            "<html><head><title>{{ T }}</title></head><body><h1>{{ T }}</h1></body></html>",
            @"{""T"":""Doc""}");

        yield return Case("table-attribute-directives",
            "<table><tbody><tr [for]=\"Rows\"><td>{{ N }}</td><td [if]=\"N == 2\">two</td></tr></tbody></table>",
            @"{""Rows"":[{""N"":1},{""N"":2}]}");

        yield return Case("pre-whitespace", "<pre>line1\n  line2\n    line3</pre>", "{}");

        yield return Case("doctype-and-attr-case", "<!DOCTYPE html><div DATA-X=\"v\" Class=\"c\">t</div>", "{}");

        yield return Case("attr-entities", "<a href=\"/s?a=1&amp;b=2\" title=\"caf&eacute;\">x</a>", "{}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Output_Matches_Golden(string name, string template, string json)
    {
        var model = JsonDocument.Parse(json).RootElement;
        var actual = await HtmlBuilder.Default.BuildFromTemplateAsync(template, model);

        var path = GoldenPath(name);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, actual);
            return;
        }

        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static object[] Case(string name, string template, string json) => new object[] { name, template, json };

    private static string GoldenPath(string name, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile), "golden", name + ".expected.html");
}
