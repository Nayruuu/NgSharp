namespace NgSharp.Ast
{
    // Else is rendered when Condition is false: null for a plain @if; an @else body, or a nested
    // IfNode for an @else if chain.
    internal sealed record IfNode(Expression Condition, TemplateNode Body, TemplateNode Else = null) : TemplateNode;
}
