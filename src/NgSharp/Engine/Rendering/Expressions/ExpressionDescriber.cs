using System.Text.Json;

using NgSharp.Ast;

namespace NgSharp.Rendering;

// Renders an expression back to template-ish text for strict-mode error messages — cold paths only,
// never on a render's happy path. Best effort: literal quoting and spacing are canonical, not the
// author's original spelling (a '?.'-guarded path prints in its normalized 'a.b' form).
internal static class ExpressionDescriber
{
    public static string Describe(Expression expression)
    {
        switch (expression)
        {
            case PathExpression path:
                return path.Path;

            case LiteralExpression literal:
                return DescribeLiteral(literal.Value);

            case PipeExpression pipe:
                var argument = pipe.LiteralArgument is not null
                    ? $":'{pipe.LiteralArgument}'"
                    : pipe.Arguments.Count > 0 ? $":{Describe(pipe.Arguments[0])}" : string.Empty;
                return $"{Describe(pipe.Source)} | {pipe.PipeName}{argument}";

            case ComparisonExpression comparison:
                return $"{Describe(comparison.Left)} {comparison.Operator} {Describe(comparison.Right)}";

            case LogicalExpression logical:
                return $"{Describe(logical.Left)} {logical.Operator} {Describe(logical.Right)}";

            case ArithmeticExpression arithmetic:
                return $"{Describe(arithmetic.Left)} {arithmetic.Operator} {Describe(arithmetic.Right)}";

            case NotExpression not:
                return $"!{Describe(not.Operand)}";

            case TernaryExpression ternary:
                return $"{Describe(ternary.Condition)} ? {Describe(ternary.WhenTrue)} : {Describe(ternary.WhenFalse)}";

            default:
                return "<expression>";
        }
    }

    private static string DescribeLiteral(NgElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return $"'{value.GetString()}'";
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return "null";
            default:
                return value.GetString() ?? "null";
        }
    }
}
