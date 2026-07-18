using System;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;

namespace NgSharp.Rendering
{
    internal static class ExpressionEvaluator
    {
        public static NgElement Evaluate(Expression expression, NgElement context, IReadOnlyDictionary<string, IPipe> pipes = null, string tagName = null)
        {
            switch (expression)
            {
                case LiteralExpression literal:
                    return literal.Value;

                case PathExpression path:
                    return ResolvePath(path.Path, context);

                case ComparisonExpression comparison:
                    return EvaluateComparison(comparison, context, pipes, tagName);

                case LogicalExpression logical:
                    return EvaluateLogical(logical, context, pipes, tagName);

                case TernaryExpression ternary:
                    var condition = Evaluate(ternary.Condition, context, pipes, tagName);
                    var branch = (condition.GetBoolean() ?? false) ? ternary.WhenTrue : ternary.WhenFalse;
                    return Evaluate(branch, context, pipes, tagName);

                case PipeExpression pipe:
                    return EvaluatePipe(pipe, context, pipes, tagName);

                default:
                    return NgElement.Null;
            }
        }

        private static NgElement EvaluateComparison(ComparisonExpression comparison, NgElement context, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
        {
            var left = Evaluate(comparison.Left, context, pipes, tagName);
            var right = Evaluate(comparison.Right, context, pipes, tagName);

            bool result;
            switch (comparison.Operator)
            {
                case "==":
                    result = left.Equals(right);
                    break;
                case "!=":
                    result = !left.Equals(right);
                    break;
                case "<":
                    result = Compare(left, right) < 0;
                    break;
                case ">":
                    result = Compare(left, right) > 0;
                    break;
                case "<=":
                    result = Compare(left, right) <= 0;
                    break;
                case ">=":
                    result = Compare(left, right) >= 0;
                    break;
                default:
                    result = false;
                    break;
            }

            return result ? NgElement.True : NgElement.False;
        }

        // Short-circuit boolean logic. Truthiness is strict: GetBoolean() ?? false — a non-boolean
        // (null, missing, number, string) is falsy, never coerced. Null checks use '!= null'.
        private static NgElement EvaluateLogical(LogicalExpression logical, NgElement context, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
        {
            var leftTruthy = Evaluate(logical.Left, context, pipes, tagName).GetBoolean() ?? false;

            if (logical.Operator == "&&" && !leftTruthy)
            {
                return NgElement.False;
            }

            if (logical.Operator == "||" && leftTruthy)
            {
                return NgElement.True;
            }

            var rightTruthy = Evaluate(logical.Right, context, pipes, tagName).GetBoolean() ?? false;

            return rightTruthy ? NgElement.True : NgElement.False;
        }

        private static NgElement EvaluatePipe(PipeExpression pipe, NgElement context, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
        {
            var value = Evaluate(pipe.Source, context, pipes, tagName);

            if (pipes == null || !pipes.TryGetValue(pipe.PipeName, out var impl))
            {
                throw new InvalidOperationException(
                    $"Unknown pipe '{pipe.PipeName}'. Register it with RegisterPipe<T>() before rendering.");
            }

            var argument = pipe.Arguments.Count > 0
                ? Evaluate(pipe.Arguments[0], context, pipes, tagName).GetString()
                : null;

            return Text(impl.Transform(tagName, value, argument));
        }

        // Resolves a path against the current context, walking up the parent chain so a
        // [for] / @for body can still see outer scope (e.g. "Shared.X" from inside a loop item).
        private static NgElement ResolvePath(string path, NgElement context)
        {
            var current = context;

            while (current != null)
            {
                var found = current.SelectToken(path);
                if (found != null)
                {
                    return found;
                }

                current = current.Parent;
            }

            return NgElement.Null;
        }

        private static int Compare(NgElement left, NgElement right)
        {
            var a = left.GetDouble() ?? 0;
            var b = right.GetDouble() ?? 0;

            return a.CompareTo(b);
        }

        // Wraps a pipe's string output as a String NgElement without NgElement.Parse's
        // literal coercion (so "12,345" stays the string "12,345", not a number).
        private static NgElement Text(string value)
        {
            return NgElement.FromStringLiteral(value);
        }
    }
}
