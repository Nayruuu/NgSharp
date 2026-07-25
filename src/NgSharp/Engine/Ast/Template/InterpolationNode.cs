namespace NgSharp.Ast;

// Raw (text-mode) interpolations render their value VERBATIM; HTML-mode ones (the default) are escaped.
internal sealed record InterpolationNode(Expression Expression, bool Raw = false) : TemplateNode;
