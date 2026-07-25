namespace NgSharp.Ast;

// '+' (numeric addition, or concatenation when either operand is a string), '-', '*', '/', '%'.
// A non-number operand counts as 0; division/modulo by zero yields 0 (never Infinity/NaN).
internal sealed record ArithmeticExpression(Expression Left, string Operator, Expression Right) : Expression;
