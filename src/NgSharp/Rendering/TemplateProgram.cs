using System.Text;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp.Rendering
{
    // Ahead-of-render compile pass — partial evaluation of the template (the "first Futamura projection"
    // by data-structure specialization, no codegen). The static skeleton of a template is invariant
    // across renders, so we fold it once: text, comments and the open/close tags of fixed-attribute
    // elements collapse into precomputed ConstNode strings, and only the data-dependent nodes
    // (interpolations, @if/@for/@not-empty, bound/directive elements, components) survive as work.
    //
    // A repeated render then pays for the dynamic parts only — one Append per static run instead of a
    // full tree walk with per-node dispatch and tag re-serialization. Output is byte-identical to
    // walking the raw AST (every branch mirrors the renderer, reusing its escaping helpers). The pass
    // itself costs about one static render, so it is worth it only when amortized: HtmlBuilder.Compile
    // (render-many) runs it; a one-shot render does not.
    internal static class TemplateProgram
    {
        public static IReadOnlyList<TemplateNode> Compile(IReadOnlyList<TemplateNode> nodes)
        {
            var emitter = new Emitter();
            CompileList(nodes, emitter);
            return emitter.Finish();
        }

        private static void CompileList(IReadOnlyList<TemplateNode> nodes, Emitter emitter)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                CompileNode(nodes[i], emitter);
            }
        }

        private static void CompileNode(TemplateNode node, Emitter emitter)
        {
            switch (node)
            {
                case TextNode text:
                    emitter.Const.Append(HtmlEscaper.EscapeText(text.Text));
                    break;

                case CommentNode comment:
                    emitter.Const.Append("<!--").Append(comment.Text).Append("-->");
                    break;

                case ElementNode element:
                    CompileElement(element, emitter);
                    break;

                case InterpolationNode interpolation:
                    emitter.Emit(interpolation);
                    break;

                case IfNode ifNode:
                    emitter.Emit(new IfNode(
                        ifNode.Condition,
                        CompileBody(ifNode.Body),
                        ifNode.Else == null ? null : CompileBody(ifNode.Else)));
                    break;

                case ForNode forNode:
                    emitter.Emit(new ForNode(forNode.Collection, CompileBody(forNode.Body)));
                    break;

                case NotEmptyNode notEmpty:
                    emitter.Emit(new NotEmptyNode(notEmpty.Collection, CompileBody(notEmpty.Body)));
                    break;

                case ComponentNode component:
                    emitter.Emit(component);
                    break;
            }
        }

        private static void CompileElement(ElementNode element, Emitter emitter)
        {
            // Transparent wrapper (@if/@for preprocessor artifact): emits its children only, so inline
            // them straight into the current static run.
            if (element.TagName == "ng-container")
            {
                CompileList(element.Children, emitter);
                return;
            }

            var hasBindings = element.Bindings.Count > 0;
            var hasDirectives = element.Directives != null && element.Directives.Count > 0;
            var rawText = TemplateRenderer.IsRawTextElement(element.TagName);

            // Opaque element: its attributes/content depend on data (bindings/directives) or must not be
            // escaped (rawtext <script>/<style>). Keep the node for the renderer, but still compile its
            // children so nested static runs fold — except inside rawtext, where folding would wrongly
            // route the text through the HTML escaper.
            if (hasBindings || hasDirectives || rawText)
            {
                var children = rawText ? element.Children : Compile(element.Children);
                emitter.Emit(new ElementNode(
                    element.TagName, element.Attributes, element.Bindings, element.Directives, children));
                return;
            }

            // Fixed-attribute element: linearize it. The open tag and close tag are constant, so they
            // merge into the surrounding run; only genuinely dynamic children break it.
            TemplateRenderer.AppendStaticOpenTag(emitter.Const, element);

            if (TemplateRenderer.IsVoidElement(element.TagName))
            {
                return;
            }

            CompileList(element.Children, emitter);
            TemplateRenderer.AppendStaticCloseTag(emitter.Const, element);
        }

        // Compiles a control-flow body (a single node, typically an ng-container) into a flat program:
        // one node when it collapses to a single instruction, otherwise a FragmentNode.
        private static TemplateNode CompileBody(TemplateNode body)
        {
            var emitter = new Emitter();
            CompileNode(body, emitter);
            var compiled = emitter.Finish();
            return compiled.Count == 1 ? compiled[0] : new FragmentNode(compiled);
        }

        // Accumulates static output in Const; Emit flushes it to a ConstNode before appending a dynamic
        // node, so the interleaving of constant and dynamic output is preserved exactly.
        private sealed class Emitter
        {
            public readonly StringBuilder Const = new StringBuilder();

            private readonly List<TemplateNode> output = new List<TemplateNode>();

            public void Emit(TemplateNode node)
            {
                Flush();
                output.Add(node);
            }

            public IReadOnlyList<TemplateNode> Finish()
            {
                Flush();
                return output;
            }

            private void Flush()
            {
                if (Const.Length > 0)
                {
                    output.Add(new ConstNode(Const.ToString()));
                    Const.Clear();
                }
            }
        }
    }
}
