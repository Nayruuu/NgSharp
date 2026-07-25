using System;
using System.Text;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Rendering;

namespace NgSharp.Parsing;

// The fused single-pass parser: the scan drives a fold emitter directly, so the template string goes
// straight to the FOLDED TemplateNode program. HTML semantics must replicate the staged pipeline
// EXACTLY — it remains the differential-tested reference. Element layer:
// TemplateParser.Fused.Elements.cs; scan primitives: TemplateParser.Fused.Scanning.cs.
internal static partial class TemplateParser
{
    private static IReadOnlyList<TemplateNode> ParseRootsFused(string html, HashSet<string> components, HashSet<string> directives)
    {
        var expanded = ControlFlowPreprocessor.Expand(html ?? string.Empty);
        var pos = 0;
        var openNames = new List<string>();
        var emitter = new FoldEmitter();

        EmitChildrenFused(emitter, expanded, ref pos, openNames, components, directives);

        return emitter.Finish();
    }

    // Emit flushes the pending Const run to a ConstNode first — const/dynamic interleaving is preserved
    // exactly.
    private sealed class FoldEmitter
    {
        // Per-thread builder pool. Emitters nest stack-like (a body Finishes before its parent resumes), so
        // release-on-Finish is safe; an abandoned emitter (parse error) just lets its builder be GC'd —
        // never a double release.
        [ThreadStatic]
        private static Stack<StringBuilder> pool;

        public readonly StringBuilder Const;

        private readonly List<TemplateNode> _output = new List<TemplateNode>();

        public FoldEmitter()
        {
            var stack = pool;
            Const = stack is not null && stack.Count > 0 ? stack.Pop() : new StringBuilder(256);
        }

        public void Emit(TemplateNode node)
        {
            Flush();
            _output.Add(node);
        }

        public List<TemplateNode> Finish()
        {
            Flush();
            (pool ??= new Stack<StringBuilder>()).Push(Const);

            return _output;
        }

        private void Flush()
        {
            if (Const.Length > 0)
            {
                _output.Add(new ConstNode(Const.ToString()));
                Const.Clear();
            }
        }
    }

    private static void EmitChildrenFused(FoldEmitter emitter, string source, ref int pos, List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        while (pos < source.Length)
        {
            if (source[pos] == '<' && IsMarkupStartF(source, pos))
            {
                if (StartsWithF(source, pos, "<!--"))
                {
                    var start = pos + 4;
                    var end = source.IndexOf("-->", start, StringComparison.Ordinal);
                    var text = end < 0 ? source.Substring(start) : source.Substring(start, end - start);

                    emitter.Const.Append("<!--").Append(text).Append("-->");
                    pos = end < 0 ? source.Length : end + 3;
                    continue;
                }

                if (source[pos + 1] == '!')
                {
                    // Declaration (<!doctype …>): dropped.
                    var end = source.IndexOf('>', pos);
                    pos = end < 0 ? source.Length : end + 1;
                    continue;
                }

                if (source[pos + 1] == '/')
                {
                    ScanCloseTagF(source, pos, out var nameStart, out var nameLen, out var after);

                    if (openNames.Count > 0 && SpanNameEqualsF(source, nameStart, nameLen, openNames[openNames.Count - 1]))
                    {
                        pos = after;

                        return;
                    }

                    if (StackContainsSpanF(openNames, source, nameStart, nameLen, openNames.Count - 1))
                    {
                        return;         // implicit close — leave the tag unconsumed for the matching ancestor
                    }

                    pos = after;        // stray close: ignored
                    continue;
                }

                // Span fast path — a non-trivial tag bails out unconsumed and falls through to the full path.
                if (TryEmitTrivialTag(emitter, source, ref pos, openNames, components, directives))
                {
                    continue;
                }

                var tagStart = pos;
                ReadTagHeaderF(source, ref pos, out var tagRawName, out var tagAttrs, out var tagSelfClosing);
                var node = EmitElementFused(emitter, source, ref pos, tagRawName, tagAttrs, tagSelfClosing, openNames, components, directives, tagStart);

                if (node is IfNode ifNode)
                {
                    var elseBranch = ChainElseFused(source, ref pos, openNames, components, directives);
                    if (elseBranch is not null)
                    {
                        node = new IfNode(ifNode.Condition, ifNode.Body, elseBranch);
                    }
                }

                if (node is not null)
                {
                    emitter.Emit(node);
                }

                continue;
            }

            var textStart = pos;
            while (pos < source.Length && (source[pos] == '<' && IsMarkupStartF(source, pos)) == false)
            {
                pos++;
            }

            EmitTextFolded(emitter, source.Substring(textStart, pos - textStart), raw: false, sourceOffset: textStart);
        }
    }

    // Literal segments fold into the const run — escaped in HTML mode, VERBATIM when raw (text mode) —
    // and {{ }} interpolations emit (Raw ones render unescaped). Same split rules as AppendText (incl.
    // the newline-in-body literal guard and the {{- / -}} whitespace-control markers — both scanners
    // must stay byte-identical for the differential oracle). sourceOffset = the run's start in the
    // parsed source, so validation diagnostics can point at the exact '{{' (only failure branches read
    // the collector).
    private static void EmitTextFolded(FoldEmitter emitter, string text, bool raw, int sourceOffset = 0)
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
                    var head = text.Substring(last, headEnd - last);
                    emitter.Const.Append(raw ? head : HtmlEscaper.EscapeText(head));
                }
            }

            if (inner.Length == 0 && (trimBefore || trimAfter))
            {
                // The deliberate whitespace eater '{{- -}}': trims both sides, emits nothing (the const
                // run continues unbroken), no empty-interpolation diagnostic.
                last = SkipTrimmedWhitespace(text, close + 2, trimAfter);
                pos = last;
                continue;
            }

            if (inner.Length == 0 && DiagnosticCollector.Current is { } collector)
            {
                collector.ReportExpanded(DiagnosticSeverity.Error, "Empty interpolation '{{ }}' — it renders nothing.", sourceOffset + open);
                collector.SuppressNextEmptyExpression();
            }

            emitter.Emit(new InterpolationNode(ExpressionParser.Parse(inner), raw));
            last = SkipTrimmedWhitespace(text, close + 2, trimAfter);
            pos = last;
        }

        if (last < text.Length)
        {
            var tail = text.Substring(last);
            emitter.Const.Append(raw ? tail : HtmlEscaper.EscapeText(tail));
        }
    }
}
