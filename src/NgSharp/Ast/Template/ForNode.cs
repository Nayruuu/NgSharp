namespace NgSharp.Ast
{
    internal sealed record ForNode(Expression Collection, TemplateNode Body) : TemplateNode;
}
