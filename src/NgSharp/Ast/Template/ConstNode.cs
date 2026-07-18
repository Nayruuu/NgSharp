namespace NgSharp.Ast
{
    // A run of static output (text, comments, fixed-attribute element tags) folded once by
    // TemplateProgram into a single precomputed, already-escaped string. The renderer appends it
    // verbatim — no per-node dispatch, no re-escaping, no tag re-serialization.
    internal sealed record ConstNode(string Text) : TemplateNode;
}
