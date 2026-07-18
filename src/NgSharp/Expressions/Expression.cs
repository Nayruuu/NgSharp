using System.Collections.Generic;

namespace NgSharp.Expressions
{
    public abstract class Expression
    {
    }

    public sealed class LiteralExpression : Expression
    {
        public NgElement Value { get; }

        public LiteralExpression(NgElement value)
        {
            Value = value;
        }
    }

    public sealed class PathExpression : Expression
    {
        public string Path { get; }

        public PathExpression(string path)
        {
            Path = path;
        }
    }

    public sealed class ComparisonExpression : Expression
    {
        public Expression Left { get; }

        public string Operator { get; }

        public Expression Right { get; }

        public ComparisonExpression(Expression left, string op, Expression right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }

    // A logical '&&' or '||' between two boolean-valued operands. Evaluated with short-circuit;
    // operand truthiness is strict (GetBoolean() ?? false) — null is not coerced, use '!= null'.
    public sealed class LogicalExpression : Expression
    {
        public Expression Left { get; }

        public string Operator { get; }

        public Expression Right { get; }

        public LogicalExpression(Expression left, string op, Expression right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }

    public sealed class TernaryExpression : Expression
    {
        public Expression Condition { get; }

        public Expression WhenTrue { get; }

        public Expression WhenFalse { get; }

        public TernaryExpression(Expression condition, Expression whenTrue, Expression whenFalse)
        {
            Condition = condition;
            WhenTrue = whenTrue;
            WhenFalse = whenFalse;
        }
    }

    public sealed class PipeExpression : Expression
    {
        public Expression Source { get; }

        public string PipeName { get; }

        public IReadOnlyList<Expression> Arguments { get; }

        public PipeExpression(Expression source, string pipeName, IReadOnlyList<Expression> arguments)
        {
            Source = source;
            PipeName = pipeName;
            Arguments = arguments;
        }
    }
}
