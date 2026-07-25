using System;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Parsing;

internal static partial class TemplateParser
{
    #region Public methods

    // No <html>/<head>/<body> auto-wrapping — the template is parsed exactly as written.
    public static IReadOnlyList<TemplateNode> ParseDocument(string html, IEnumerable<string> componentNames = null, IEnumerable<string> directiveNames = null)
        => ParseRoots(html, componentNames, directiveNames);

    #endregion

    #region Private methods

    private static IReadOnlyList<TemplateNode> ParseRoots(string html, IEnumerable<string> componentNames, IEnumerable<string> directiveNames)
        => ParseRootsFused(html, Set(componentNames), Set(directiveNames));

    private static HashSet<string> Set(IEnumerable<string> names)
        => names is null ? new HashSet<string>() : new HashSet<string>(names);

    #endregion

    #region Text-splitting primitives

    // {{- / -}} whitespace-control markers (Scriban/Liquid-style). A marker is only active GLUED to its
    // braces WITH a whitespace on the expression side: '{{- x }}' trims before, '{{ x -}}' trims after.
    // The rule is what keeps negation safe: '{{ -X }}' (dash not glued to the braces) and '{{-X }}' (no
    // whitespace after the dash) are ordinary expressions. Reading text[open + 3] is safe: the dash at
    // open + 2 implies the '}}' close sits at open + 3 or later.
    private static void DetectTrimMarkers(string text, int open, int close, out bool trimBefore, out bool trimAfter)
    {
        trimBefore = text[open + 2] == '-' && char.IsWhiteSpace(text[open + 3]);
        trimAfter = text[close - 1] == '-' && close - 2 >= open + 2 && char.IsWhiteSpace(text[close - 2]);
    }

    // '-}}' eats the whitespace (newlines included) that follows, within the current text run.
    private static int SkipTrimmedWhitespace(string text, int from, bool trimAfter)
    {
        if (trimAfter)
        {
            while (from < text.Length && char.IsWhiteSpace(text[from]))
            {
                from++;
            }
        }

        return from;
    }

    #endregion

    #region Internal methods

    // The "[prefix.target]" binding family ([attr.x] / [style.x] / [class.x]). Internal (not private):
    // the staged reference pipeline (StagedTemplateParser, test assembly) shares it so both walks stay
    // rule-identical for the differential oracle.
    internal static bool TryPrefixedBinding(string name, string prefix, BindingKind kind, string value, ref List<BindingNode> bindings)
    {
        if (name.StartsWith(prefix) == false || name.EndsWith("]") == false)
        {
            return false;
        }

        var target = name.Substring(prefix.Length, name.Length - prefix.Length - 1);
        (bindings ??= new List<BindingNode>()).Add(new BindingNode(kind, target, ExpressionParser.Parse(value)));

        return true;
    }

    // Splits a text run into TextNode / InterpolationNode on {{ }}; a {{ }} whose trimmed body contains
    // a newline stays literal. {{- / -}} whitespace-control markers trim the adjacent whitespace within
    // the run (see DetectTrimMarkers). sourceOffset feeds validation diagnostics only (rawtext children
    // pass the run's start; the staged reference pipeline never validates and keeps the default).
    // Internal (not private): shared with the staged reference pipeline (test assembly) so both text
    // scanners stay rule-identical for the differential oracle.
    internal static void AppendText(List<TemplateNode> nodes, string text, int sourceOffset = 0)
    {
        var last = 0;
        var pos = 0;

        while (true)
        {
            var open = text.IndexOf("{{", pos, StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            var close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                DiagnosticCollector.Current?.ReportExpanded(DiagnosticSeverity.Error,
                    "Unclosed interpolation '{{' — no matching '}}', so it renders as literal text.", sourceOffset + open);
                break;
            }

            DetectTrimMarkers(text, open, close, out var trimBefore, out var trimAfter);

            var innerStart = open + 2 + (trimBefore ? 1 : 0);
            var innerEnd = close - (trimAfter ? 1 : 0);
            var inner = text.Substring(innerStart, innerEnd - innerStart).Trim();
            if (inner.IndexOf('\n') >= 0)
            {
                DiagnosticCollector.Current?.ReportExpanded(DiagnosticSeverity.Warning,
                    "A '{{ … }}' whose body spans a line break is kept as literal text — keep the interpolation on one line.", sourceOffset + open);
                pos = open + 2;
                continue;
            }

            if (open > last)
            {
                var headEnd = open;
                if (trimBefore)
                {
                    while (headEnd > last && char.IsWhiteSpace(text[headEnd - 1]))
                    {
                        headEnd--;
                    }
                }

                if (headEnd > last)
                {
                    nodes.Add(new TextNode(text.Substring(last, headEnd - last)));
                }
            }

            if (inner.Length == 0 && (trimBefore || trimAfter))
            {
                // The deliberate whitespace eater '{{- -}}': trims both sides, emits nothing, no
                // empty-interpolation diagnostic.
                last = SkipTrimmedWhitespace(text, close + 2, trimAfter);
                pos = last;
                continue;
            }

            if (inner.Length == 0 && DiagnosticCollector.Current is { } collector)
            {
                collector.ReportExpanded(DiagnosticSeverity.Error, "Empty interpolation '{{ }}' — it renders nothing.", sourceOffset + open);
                collector.SuppressNextEmptyExpression();
            }

            nodes.Add(new InterpolationNode(ExpressionParser.Parse(inner)));
            last = SkipTrimmedWhitespace(text, close + 2, trimAfter);
            pos = last;
        }

        if (last < text.Length)
        {
            nodes.Add(new TextNode(text.Substring(last)));
        }
    }

    #endregion
}
