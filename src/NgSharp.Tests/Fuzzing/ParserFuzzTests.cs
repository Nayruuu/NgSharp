using System;
using System.Text;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Parsing;
using NgSharp.Rendering;

namespace NgSharp.Tests.Fuzzing;

// Structural fuzzing of the parser front door: deterministic (hand-rolled xorshift PRNG, fixed
// seeds — replayable byte for byte) mutated templates made of valid/malformed HTML, broken
// interpolations, mis-nested @-blocks, raw text-dialect sentinels, unknown directives and mixed
// dialect fragments. Five contracts hold on EVERY input: (a) the lenient parse never throws, either
// dialect; (b) Validate never throws and terminates; (c) the lenient render on an empty model only
// ever throws NgSharpException where contractual (unknown pipe) — or the characterized NumberPipe
// fossil (InvalidOperationException on a non-numeric value, pinned by BuiltInPipeTests); (d) fused
// and staged parses render byte-identically — the differential oracle extended to hostile territory;
// (e) the STRICT render only ever fails through the same two exception types — hostile input cannot
// break the strict contract either.
public class ParserFuzzTests
{
    #region Corpus

    private static readonly string[] Fragments =
    {
        // Valid markup, structural attributes, components-shaped tags.
        "<div class=\"a\">", "</div>", "<span>", "</span>", "<p [if]=\"Show\">x</p>",
        "<br/>", "<img src=\"x.png\">", "text & more ", "<ul [not-empty]=\"Items\"><li>i</li></ul>",
        "<tr [for]=\"Items\"><td>{{ V }}</td></tr>", "<p [empty]=\"Items\">none</p>",
        "<div [else]=\"\">e</div>", "<div [else-if]=\"Hide\">ei</div>", "<em [case]=\"1\">c</em>",
        "<ng-container [switch]=\"Count\"><ng-container [case]=\"1\">one</ng-container></ng-container>",
        "<ng-template #frag>{{ Name }}</ng-template>", "@render(frag)", "<ng-container>",
        "<script>var a = 1 < 2;</script>", "<style>.x>y{}</style>",
        // Malformed markup: unclosed tags, valueless/unterminated attributes, orphan chevrons.
        "<div", "</", "<>", "< div>", "<div attr>", "<div class=>", "<div class=\"unterminated>",
        "</div", "<-", ">", "<", "<!doctype html>", "<!--", "-->", "<!-- c -->", "</wrong>",
        "<x-card>", "</x-card>", "<div [unknowndir]=\"X\">u</div>", "<DIV>", "</SPAN>",
        // Interpolations: unclosed, empty, orphan pipes, invalid expressions, trim markers.
        "{{ Name }}", "{{ Name", "}}", "{{ }}", "{{ X | }}", "{{ a b }}", "{{ X | upper }}",
        "{{ X | nope }}", "{{ 1 / 0 }}", "{{ Count % 0 }}", "{{ 'unterminated }}", "{{ ((X }}",
        "{{ X ? : }}", "{{- -}}", "{{ Name -}}", "{{- Name }}", "{{ X ?? Y }}", "{{ .. }}",
        "{{ Items[0].V }}", "{{ Items[9].V }}", "{{ -X }}", "{{ X\nY }}", "{{ | | }}",
        "{{ Name | number:'N0' }}", "{{ Name | currency:'EUR' }}",
        // @-blocks: unclosed, mis-nested, orphan branches, degenerate openers, stray braces.
        "@if (Show) {", "@if (42) {", "}", "@else {", "@else if (Hide) {", "@for (x of Items) {",
        "@for (x in Items) {", "@for (Items) {", "@switch (Count) {", "@case (1) {", "@default {",
        "@if (Show)", "@else", "@render()", "@render(", "@if () {", "{", "@if (Show) { }",
        // Raw text-dialect sentinels — untypable control chars, forged markers included.
        "\u0001", "\u0002", "\u0003", "\u0001if\u0002Show\u0003", "\u0001\u0003",
        "\u0001for\u0002Items\u0002x\u0003",
    };

    #endregion

    #region Tests

    [Theory]
    [InlineData(0x9E3779B9u)]
    [InlineData(0xC0FFEE42u)]
    [InlineData(0x1234ABCDu)]
    public void Hostile_Templates_Never_Break_The_Parse_Validate_And_Render_Contracts(uint seed)
    {
        var builder = HtmlBuilder.Create();
        var pipes = builder.Pipes;
        var richModel = NgElement.FromObject(new
        {
            Name = "Ada & Co",
            Show = true,
            Hide = false,
            Count = 3,
            X = 5,
            Items = new[] { new { V = 1 }, new { V = 2 } },
        });
        var emptyModel = NgElement.FromObject(new { });
        var state = seed;

        for (var caseIndex = 0; caseIndex < 500; caseIndex++)
        {
            var template = GenerateTemplate(ref state);
            var label = $"seed 0x{seed:X8}, case {caseIndex}, template <<{Printable(template)}>>";

            // (a) The lenient parse never throws, either dialect.
            var fused = ParseGuarded(() => TemplateParser.ParseDocument(template), label, "HTML parse");
            var text = ParseGuarded(() => TemplateParser.ParseTextDocument(template), label, "text parse");

            // (b) Validate never throws (documented contract) and terminates, either dialect.
            try
            {
                builder.Validate(template);
                builder.Validate(template, TemplateMode.Text);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"Validate threw on {label}: {exception}");
            }

            // (c) The lenient render on an empty model never throws outside the contract.
            RenderGuarded(fused, emptyModel, pipes, label, "HTML render", strict: false);
            RenderGuarded(text, emptyModel, pipes, label, "text render", strict: false);

            // (e) The strict render fails only through its contractual exception types.
            RenderGuarded(fused, emptyModel, pipes, label, "strict HTML render", strict: true);
            RenderGuarded(text, richModel, pipes, label, "strict text render", strict: true);

            // (d) Differential oracle on hostile territory: when the staged reference pipeline parses
            // the input too, both programs must render byte-identically.
            IReadOnlyList<TemplateNode> staged;
            try
            {
                staged = StagedTemplateParser.ParseRootsViaStagedPipeline(template);
            }
            catch (Exception)
            {
                continue;   // the reference pipeline is not the shipped contract — (a) already held
            }

            var fusedOutput = RenderForDiff(fused, richModel, pipes);
            var stagedOutput = RenderForDiff(staged, richModel, pipes);
            if (fusedOutput != stagedOutput)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Fused/staged divergence on {label}:\n  fused:  <<{Printable(fusedOutput)}>>\n  staged: <<{Printable(stagedOutput)}>>");
            }
        }
    }

    #endregion

    #region Private methods

    // xorshift32 — deterministic, dependency-free; the seed must be non-zero.
    private static uint NextRandom(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;

        return state;
    }

    private static string GenerateTemplate(ref uint state)
    {
        var count = 1 + (int)(NextRandom(ref state) % 16);
        var builder = new StringBuilder(count * 24);

        for (var i = 0; i < count; i++)
        {
            builder.Append(Fragments[NextRandom(ref state) % (uint)Fragments.Length]);
            if (NextRandom(ref state) % 4 == 0)
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<TemplateNode> ParseGuarded(Func<IReadOnlyList<TemplateNode>> parse, string label, string stage)
    {
        try
        {
            return parse();
        }
        catch (Exception exception)
        {
            throw new Xunit.Sdk.XunitException($"{stage} threw on {label}: {exception}");
        }
    }

    private static void RenderGuarded(IReadOnlyList<TemplateNode> nodes, NgElement model, IReadOnlyDictionary<string, IPipe> pipes, string label, string stage, bool strict)
    {
        try
        {
            TemplateRenderer.Render(nodes, model, pipes, null, null, TemplateRenderer.CollectTemplates(nodes), strict: strict);
        }
        catch (NgSharpException)
        {
            // Contractual: an unknown pipe throws at render time (lenient included), and strict adds
            // its own throws (missing path, non-boolean condition, division by zero).
        }
        catch (InvalidOperationException)
        {
            // The characterized NumberPipe fossil: a non-numeric, non-null value under number/currency.
        }
        catch (Exception exception)
        {
            throw new Xunit.Sdk.XunitException($"{stage} threw a non-contractual {exception.GetType().Name} on {label}: {exception}");
        }
    }

    // Both programs come from the same expression parser, so a contractual render throw (unknown
    // pipe) hits both sides identically — collapsing it to a marker keeps the oracle comparable.
    private static string RenderForDiff(IReadOnlyList<TemplateNode> nodes, NgElement model, IReadOnlyDictionary<string, IPipe> pipes)
    {
        try
        {
            return TemplateRenderer.Render(nodes, model, pipes, null, null, TemplateRenderer.CollectTemplates(nodes));
        }
        catch (NgSharpException)
        {
            return " <NgSharpException>";
        }
        catch (InvalidOperationException)
        {
            // The characterized NumberPipe fossil hits both programs identically (same expressions).
            return " <InvalidOperationException>";
        }
    }

    private static string Printable(string text)
        => text.Replace("\u0001", "<S>").Replace("\u0002", "<P>").Replace("\u0003", "<E>").Replace("\n", "\\n");

    #endregion
}
