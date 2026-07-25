namespace NgSharp.Ast;

// A run of static output folded into a single precomputed, ALREADY-ESCAPED string — the renderer
// appends it verbatim.
internal sealed record ConstNode(string Text) : TemplateNode;
