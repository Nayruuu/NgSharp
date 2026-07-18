using System.Collections.Generic;

namespace NgSharp.Ast
{
    internal sealed record PipeExpression(Expression Source, string PipeName, IReadOnlyList<Expression> Arguments) : Expression;
}
