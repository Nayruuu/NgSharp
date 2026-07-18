namespace NgSharp.Ast
{
    internal sealed record TernaryExpression(Expression Condition, Expression WhenTrue, Expression WhenFalse) : Expression;
}
