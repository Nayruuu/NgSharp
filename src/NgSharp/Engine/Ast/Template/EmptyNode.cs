namespace NgSharp.Ast;

// The dual of NotEmptyNode: the body renders when the collection is empty or absent.
internal sealed record EmptyNode(Expression Collection, TemplateNode Body) : TemplateNode;
