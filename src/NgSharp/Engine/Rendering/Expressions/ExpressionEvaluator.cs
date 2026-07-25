using System;
using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;

namespace NgSharp.Rendering;

internal static class ExpressionEvaluator
{
    // Resolves an expression against a scope CHAIN (outer contexts at the bottom, current on top), so an
    // inner [for]/@for binding can see outer scope.
    public static NgElement Evaluate(Expression expression, ScopeChain scope, IReadOnlyDictionary<string, IPipe> pipes = null, string tagName = null)
    {
        // Case order = render-time dispatch frequency (the type-pattern switch is a sequential isinst
        // ladder) — do NOT reorder: paths first, then pipes.
        switch (expression)
        {
            case PathExpression path:
                return ResolvePath(path, scope);

            case PipeExpression pipe:
                return EvaluatePipe(pipe, scope, pipes, tagName);

            case LiteralExpression literal:
                return literal.Value;

            case ComparisonExpression comparison:
                return EvaluateComparison(comparison, scope, pipes, tagName);

            case LogicalExpression logical:
                return EvaluateLogical(logical, scope, pipes, tagName);

            case ArithmeticExpression arithmetic:
                return EvaluateArithmetic(arithmetic, scope, pipes, tagName);

            case NotExpression not:
                // Strict truthiness, same as the logical operators: a non-boolean operand is falsy.
                var operand = Evaluate(not.Operand, scope, pipes, tagName);
                return (operand.GetBoolean() ?? false) ? NgElement.False : NgElement.True;

            case TernaryExpression ternary:
                var condition = Evaluate(ternary.Condition, scope, pipes, tagName);
                var branch = (condition.GetBoolean() ?? false) ? ternary.WhenTrue : ternary.WhenFalse;

                return Evaluate(branch, scope, pipes, tagName);

            default:
                return NgElement.Null;
        }
    }

    // Compat entry for direct callers: a single root context, no outer scope, never strict.
    public static NgElement Evaluate(Expression expression, NgElement context, IReadOnlyDictionary<string, IPipe> pipes = null, string tagName = null)
        => Evaluate(expression, new ScopeChain(1, strict: false) { new ScopeFrame(context, null) }, pipes, tagName);

    // The '==' operator's single equality rule (NgElement value equality) — shared with the
    // renderer's @switch case matching so the two sites can never drift.
    public static bool AreEqual(NgElement left, NgElement right) => left.Equals(right);

    private static NgElement EvaluateComparison(ComparisonExpression comparison, ScopeChain scope, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
    {
        var left = Evaluate(comparison.Left, scope, pipes, tagName);
        var right = Evaluate(comparison.Right, scope, pipes, tagName);

        bool result;
        switch (comparison.Operator)
        {
            case "==":
                result = AreEqual(left, right);
                break;
            case "!=":
                result = AreEqual(left, right) == false;
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

    // Short-circuit; truthiness is strict (GetBoolean() ?? false) — a non-boolean is falsy, never coerced.
    private static NgElement EvaluateLogical(LogicalExpression logical, ScopeChain scope, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
    {
        var leftTruthy = Evaluate(logical.Left, scope, pipes, tagName).GetBoolean() ?? false;

        if (logical.Operator == "&&" && leftTruthy == false)
        {
            return NgElement.False;
        }

        if (logical.Operator == "||" && leftTruthy)
        {
            return NgElement.True;
        }

        var rightTruthy = Evaluate(logical.Right, scope, pipes, tagName).GetBoolean() ?? false;

        return rightTruthy ? NgElement.True : NgElement.False;
    }

    private static NgElement EvaluateArithmetic(ArithmeticExpression arithmetic, ScopeChain scope, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
    {
        var left = Evaluate(arithmetic.Left, scope, pipes, tagName);
        var right = Evaluate(arithmetic.Right, scope, pipes, tagName);

        // '+' concatenates when either operand is a string (Angular/JS semantics); otherwise it adds.
        if (arithmetic.Operator == "+"
            && (left.ValueKind == JsonValueKind.String || right.ValueKind == JsonValueKind.String))
        {
            return NgElement.FromStringLiteral(Stringify(left) + Stringify(right));
        }

        // A non-number operand counts as 0.
        var leftNumber = left.GetDouble() ?? 0d;
        var rightNumber = right.GetDouble() ?? 0d;

        double result;
        switch (arithmetic.Operator)
        {
            case "+": result = leftNumber + rightNumber; break;
            case "-": result = leftNumber - rightNumber; break;
            case "*": result = leftNumber * rightNumber; break;
            // Guard div/mod by zero so the output is a plain 0, never "∞" or "NaN" — strict throws instead.
            case "/": result = rightNumber == 0d ? DivideOrModuloByZero(arithmetic, scope) : leftNumber / rightNumber; break;
            case "%": result = rightNumber == 0d ? DivideOrModuloByZero(arithmetic, scope) : leftNumber % rightNumber; break;
            default: result = 0d; break;
        }

        return NgElement.FromNumber(result);
    }

    // Out of line so the zero-divisor guard stays a compare + never-taken call in EvaluateArithmetic.
    // Non-strict keeps the historical contract (a plain 0, never "∞"/"NaN"); strict refuses to
    // fabricate a value and names the expression.
    private static double DivideOrModuloByZero(ArithmeticExpression arithmetic, ScopeChain scope)
    {
        if (scope.Strict)
        {
            throw new NgSharpException(
                $"Strict mode: {(arithmetic.Operator == "/" ? "division" : "modulo")} by zero in '{ExpressionDescriber.Describe(arithmetic)}' — the divisor evaluated to 0 (non-strict renders 0).");
        }

        return 0d;
    }

    private static string Stringify(NgElement value)
        => value.ValueKind == JsonValueKind.Null ? string.Empty : (value.GetString() ?? string.Empty);

    private static NgElement EvaluatePipe(PipeExpression pipe, ScopeChain scope, IReadOnlyDictionary<string, IPipe> pipes, string tagName)
    {
        var value = Evaluate(pipe.Source, scope, pipes, tagName);

        var implementation = pipe.Resolve(pipes);
        if (implementation is null)
        {
            throw new NgSharpException(
                $"Unknown pipe '{pipe.PipeName}'. Register it with RegisterPipe<T>() before rendering.");
        }

        var argument = pipe.LiteralArgument
            ?? (pipe.Arguments.Count > 0 ? Evaluate(pipe.Arguments[0], scope, pipes, tagName).GetString() : null);

        return Text(implementation.Transform(tagName, value, argument));
    }

    // Walks the chain from the current (top) frame OUT to the root, so a [for]/@for body can still see
    // outer scope (e.g. "Shared.X" from inside a loop item).
    private static NgElement ResolvePath(PathExpression path, ScopeChain scope)
    {
        var first = path.IsPlain ? path.Path : path.Segments[0];

        // $index/$count/$first/$last intercept BEFORE the frame walk — one char test, zero cost when absent.
        if (first[0] == '$')
        {
            var special = ResolveLoopVariable(first, scope);
            // Null here can ONLY be a miss (unknown $name, or no enclosing loop) — a hit is always
            // number/bool — so the strict flag is read behind that cold test, never on a hit.
            if (special.ValueKind == JsonValueKind.Null && scope.Strict && path.Guarded == false)
            {
                ThrowPathNotFound(path);
            }

            return path.IsPlain ? special : special.SelectSegments(path.Segments, 1, path);
        }

        for (var i = scope.Count - 1; i >= 0; i--)
        {
            var frame = scope[i];

            if (frame.Name is not null)
            {
                // A named @for frame is reachable ONLY by its variable name — never by implicit bare-name resolution.
                if (frame.Name == first)
                {
                    if (path.IsPlain)
                    {
                        return frame.Context;                                   // {{ p }}     -> the item itself
                    }

                    var member = frame.Context.SelectSegments(path.Segments, 1, path);  // {{ p.a.b }} -> resolve [a,b] on it
                    // Undefined = the member is ABSENT under the loop variable (a present null resolves
                    // to Null-kind) — the strict flag is only read behind that cold test.
                    if (member.IsUndefined && scope.Strict && path.Guarded == false)
                    {
                        ThrowPathNotFound(path);
                    }

                    return member;
                }

                continue;
            }

            // Implicit [for] / root frame: resolve the whole path. Only a property HIT publishes the
            // inline-cache memo, so a site resolved on an outer frame stays stable across the walk.
            var found = path.IsPlain ? frame.Context.SelectMember(path.Path, path) : frame.Context.SelectSegments(path.Segments, 0, path);
            if (found.IsUndefined == false)
            {
                return found;
            }
        }

        // Absent on EVERY frame — never confused with a present-but-null value (that returns above
        // with ValueKind Null). This is the only spot the strict flag is read on the implicit walk.
        if (scope.Strict && path.Guarded == false)
        {
            ThrowPathNotFound(path);
        }

        return NgElement.Null;
    }

    // Out of line so the strict checks stay a compare + never-taken jump inside ResolvePath.
    private static void ThrowPathNotFound(PathExpression path)
        => throw new NgSharpException(
            $"Strict mode: the path '{path.Path}' was not found in the model — no scope frame has a property of that name. "
            + "A property that IS present with a null value renders empty without throwing; for genuinely optional data, "
            + "guard the path with '?.' or render without strict.");

    // Resolves against the NEAREST enclosing loop frame (named or implicit alike — a named @for frame
    // hides its item, not its position). Outside any loop, or for an unknown $name: Null.
    private static NgElement ResolveLoopVariable(string name, ScopeChain scope)
    {
        for (var i = scope.Count - 1; i >= 0; i--)
        {
            var frame = scope[i];
            if (frame.Index < 0)
            {
                continue;
            }

            switch (name)
            {
                case "$index":
                    return NgElement.FromParsedNumber((long)frame.Index);
                case "$count":
                    return NgElement.FromParsedNumber((long)frame.Count);
                case "$first":
                    return frame.Index == 0 ? NgElement.True : NgElement.False;
                case "$last":
                    return frame.Index == frame.Count - 1 ? NgElement.True : NgElement.False;
                default:
                    return NgElement.Null;
            }
        }

        return NgElement.Null;
    }

    private static int Compare(NgElement left, NgElement right)
    {
        var leftNumber = left.GetDouble() ?? 0;
        var rightNumber = right.GetDouble() ?? 0;

        return leftNumber.CompareTo(rightNumber);
    }

    // No NgElement.Parse literal coercion: a pipe's "12,345" stays a string, never becomes a number.
    private static NgElement Text(string value)
    {
        return NgElement.FromStringLiteral(value);
    }
}
