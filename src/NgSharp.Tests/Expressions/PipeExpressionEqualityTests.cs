using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;

namespace NgSharp.Tests.Expressions;

// PipeExpression equality must ignore the private pipe memo (render-state, not identity) — the same
// invariant PathExpression already guards for its member-site cache.
public class PipeExpressionEqualityTests
{
    [Fact]
    public void Identical_PipeExpressions_Stay_Equal_After_A_Render_Populates_One_Memo()
    {
        var arguments = new List<Expression>();
        var rendered = new PipeExpression(new PathExpression("Name"), "upper", arguments);
        var pristine = new PipeExpression(new PathExpression("Name"), "upper", arguments);
        var pipes = new Dictionary<string, IPipe> { ["upper"] = new UpperPipe() };

        rendered.Resolve(pipes);   // what a render does on {{ Name | upper }}: it populates the memo

        Assert.Equal(rendered, pristine);
        Assert.Equal(pristine, rendered);
        Assert.Equal(rendered.GetHashCode(), pristine.GetHashCode());
    }
}
