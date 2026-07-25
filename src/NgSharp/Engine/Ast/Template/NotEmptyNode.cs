namespace NgSharp.Ast;

internal sealed record NotEmptyNode(Expression Collection, TemplateNode Body) : TemplateNode;
