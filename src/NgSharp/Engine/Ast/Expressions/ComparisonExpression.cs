namespace NgSharp.Ast;

internal sealed record ComparisonExpression(Expression Left, string Operator, Expression Right) : Expression;
