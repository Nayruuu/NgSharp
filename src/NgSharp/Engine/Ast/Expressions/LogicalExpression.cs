namespace NgSharp.Ast;

// A logical '&&' or '||' between two boolean-valued operands. Evaluated with short-circuit;
// operand truthiness is strict (GetBoolean() ?? false) — null is not coerced, use '!= null'.
internal sealed record LogicalExpression(Expression Left, string Operator, Expression Right) : Expression;
