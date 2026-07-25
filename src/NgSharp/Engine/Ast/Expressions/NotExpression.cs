namespace NgSharp.Ast;

// Unary logical negation '!'. Operand truthiness is strict (GetBoolean() ?? false) — a non-boolean
// (null, missing, number, string) is falsy, so '!' of it is true, matching the '&&'/'||' model.
internal sealed record NotExpression(Expression Operand) : Expression;
