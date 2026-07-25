using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Parsing;

// The TEXT-dialect parser (TemplateMode.Text): plain text + {{ }} interpolations + @if/@else/@for/
// @switch blocks — no elements, components, directives, ng-template or attribute forms. It shares the
// control-flow preprocessor with the HTML dialect and consumes the control-character sentinels
// ExpandText generates (U+0001 kind U+0002 expression U+0003 … U+0001 U+0003 — untypable, so
// author-written text can never fake or terminate a block, and expressions carry any quoting
// verbatim). Every other source character — any '<' included — is output VERBATIM (no escaping
// anywhere: const runs copy the source, interpolations emit Raw).
internal static partial class TemplateParser
{
    public static IReadOnlyList<TemplateNode> ParseTextDocument(string text)
    {
        var expanded = ControlFlowPreprocessor.ExpandText(text ?? string.Empty);
        var pos = 0;
        var emitter = new FoldEmitter();

        EmitTextModeChildren(emitter, expanded, ref pos, root: true);

        return emitter.Finish();
    }

    // Scans one children list: verbatim text/interpolations plus block sentinels. A close sentinel
    // ends the list (consumed); the preprocessor's pairs are balanced, so root never sees one — a
    // stray Start char there is author bytes and stays literal text (the pos++ fall-through).
    private static void EmitTextModeChildren(FoldEmitter emitter, string source, ref int pos, bool root)
    {
        var textStart = pos;

        while (pos < source.Length)
        {
            if (source[pos] == ControlFlowPreprocessor.TEXT_MARKER_START)
            {
                if (root == false
                    && pos + 1 < source.Length && source[pos + 1] == ControlFlowPreprocessor.TEXT_MARKER_END)
                {
                    EmitTextFolded(emitter, source.Substring(textStart, pos - textStart), raw: true, sourceOffset: textStart);
                    pos += 2;

                    return;
                }

                // [else]/[else-if] sentinels are only consumed in chain position, right after an @if
                // body; [case]/[default] only inside an @switch body (BuildTextModeSwitch).
                if (TryReadTextMarker(source, pos, out var kind, out var expression, out var extra, out var after)
                    && (kind == "if" || kind == "for" || kind == "render" || kind == "switch"))
                {
                    EmitTextFolded(emitter, source.Substring(textStart, pos - textStart), raw: true, sourceOffset: textStart);

                    var markerStart = pos;
                    pos = after;
                    emitter.Emit(BuildTextModeBlock(source, ref pos, kind, expression, extra, markerStart));
                    textStart = pos;
                    continue;
                }
            }

            pos++;
        }

        EmitTextFolded(emitter, source.Substring(textStart, pos - textStart), raw: true, sourceOffset: textStart);
    }

    // One desugared block to its node; pos is past the open sentinel on entry, past the block's close
    // on exit. markerStart is the open sentinel's offset — validation diagnostics only.
    private static TemplateNode BuildTextModeBlock(string source, ref int pos, string kind, string expression, string extra, int markerStart)
    {
        if (kind == "for")
        {
            // extra is the [for-var] name from @for (Var of Collection); null for the implicit form.
            return new ForNode(ExpressionParser.Parse(expression), extra, ParseTextModeBody(source, ref pos));
        }

        if (kind == "render")
        {
            // Text mode defines no <ng-template>, so the outlet renders nothing — HTML's silent contract.
            ParseTextModeBody(source, ref pos);   // the machine-generated marker is empty; consumed, discarded

            return new RenderTemplateNode(expression, extra is null ? null : ExpressionParser.Parse(extra));
        }

        if (kind == "switch")
        {
            return BuildTextModeSwitch(source, ref pos, expression);
        }

        var body = ParseTextModeBody(source, ref pos);
        var condition = ExpressionParser.Parse(expression);
        DiagnosticCollector.Current?.CheckIfCondition(condition, expression, markerStart);

        return new IfNode(condition, body, ChainTextModeElse(source, ref pos));
    }

    // The @switch body scan (text dialect): only @case/@default sentinels and whitespace are legal —
    // anything else is consumed, DROPPED, and (validation only) reported as stray. pos is past the
    // switch's open sentinel on entry, past its close sentinel on exit.
    private static TemplateNode BuildTextModeSwitch(string source, ref int pos, string switchExpression)
    {
        var cases = new List<SwitchCase>();
        TemplateNode defaultBody = null;

        while (pos < source.Length)
        {
            if (source[pos] == ControlFlowPreprocessor.TEXT_MARKER_START)
            {
                if (pos + 1 < source.Length && source[pos + 1] == ControlFlowPreprocessor.TEXT_MARKER_END)
                {
                    pos += 2;

                    break;   // the switch's own close sentinel
                }

                if (TryReadTextMarker(source, pos, out var kind, out var expression, out var extra, out var after))
                {
                    if (kind == "case")
                    {
                        pos = after;
                        cases.Add(new SwitchCase(ExpressionParser.Parse(expression), ParseTextModeBody(source, ref pos)));
                        continue;
                    }

                    if (kind == "default")
                    {
                        pos = after;
                        var body = ParseTextModeBody(source, ref pos);
                        defaultBody ??= body;   // first @default wins
                        continue;
                    }

                    // A non-arm block (an @if/@for/@render directly between the switch's braces):
                    // stray — consumed whole (else chain included) and dropped.
                    ReportStraySwitchContent(pos);
                    var strayStart = pos;
                    pos = after;
                    BuildTextModeBlock(source, ref pos, kind, expression, extra, strayStart);
                    continue;
                }
            }

            // Interstitial bytes (a stray Start char included): whitespace is legal, anything else is
            // stray — either way the run never renders.
            var textStart = pos;
            pos++;
            while (pos < source.Length && source[pos] != ControlFlowPreprocessor.TEXT_MARKER_START)
            {
                pos++;
            }

            ReportStraySwitchText(source, textStart, pos);
        }

        return new SwitchNode(ExpressionParser.Parse(switchExpression), cases, defaultBody);
    }

    // The preprocessor emits a chained @else / @else if sentinel IMMEDIATELY after its @if's close
    // sentinel (the interstice is consumed at desugar time), so the chain is an adjacency check.
    private static TemplateNode ChainTextModeElse(string source, ref int pos)
    {
        if (TryReadTextMarker(source, pos, out var kind, out var expression, out _, out var after) == false)
        {
            return null;
        }

        if (kind == "else-if")
        {
            var markerStart = pos;
            pos = after;
            var body = ParseTextModeBody(source, ref pos);
            var condition = ExpressionParser.Parse(expression);
            DiagnosticCollector.Current?.CheckIfCondition(condition, expression, markerStart);

            return new IfNode(condition, body, ChainTextModeElse(source, ref pos));
        }

        if (kind == "else")
        {
            pos = after;

            return ParseTextModeBody(source, ref pos);
        }

        return null;
    }

    // A block body folds into its own fresh run and collapses (CompileBody semantics).
    private static TemplateNode ParseTextModeBody(string source, ref int pos)
    {
        var emitter = new FoldEmitter();

        EmitTextModeChildren(emitter, source, ref pos, root: false);

        return SingleOrFragment(emitter.Finish());
    }

    // Reads one machine-generated open sentinel: Start kind Sep expression (Sep extra)? End — the
    // expression and extra are VERBATIM slices, quotes and all. Strict by design: any deviation
    // (a close sentinel included: no Sep before its End) is not a marker; pos is untouched, after
    // points past the End.
    private static bool TryReadTextMarker(string source, int pos, out string kind, out string expression, out string extra, out int after)
    {
        kind = null;
        expression = null;
        extra = null;
        after = pos;

        if (pos >= source.Length || source[pos] != ControlFlowPreprocessor.TEXT_MARKER_START)
        {
            return false;
        }

        var j = pos + 1;
        var kindStart = j;
        while (j < source.Length
               && source[j] != ControlFlowPreprocessor.TEXT_MARKER_SEPARATOR
               && source[j] != ControlFlowPreprocessor.TEXT_MARKER_END)
        {
            j++;
        }

        if (j >= source.Length || source[j] != ControlFlowPreprocessor.TEXT_MARKER_SEPARATOR)
        {
            return false;
        }

        kind = source.Substring(kindStart, j - kindStart);
        j++;

        var expressionStart = j;
        while (j < source.Length
               && source[j] != ControlFlowPreprocessor.TEXT_MARKER_SEPARATOR
               && source[j] != ControlFlowPreprocessor.TEXT_MARKER_END)
        {
            j++;
        }

        if (j >= source.Length)
        {
            return false;
        }

        expression = source.Substring(expressionStart, j - expressionStart);

        if (source[j] == ControlFlowPreprocessor.TEXT_MARKER_SEPARATOR)
        {
            j++;
            var extraStart = j;
            while (j < source.Length && source[j] != ControlFlowPreprocessor.TEXT_MARKER_END)
            {
                j++;
            }

            if (j >= source.Length)
            {
                return false;
            }

            extra = source.Substring(extraStart, j - extraStart);
        }

        after = j + 1;

        return true;
    }
}
