using System.Collections.Generic;

namespace NgSharp.Ast
{
    // A compiled node list used as the body of a control-flow node (@if / @for / @not-empty) once
    // TemplateProgram has flattened the ng-container wrapper the parser produced. Rendered as its
    // children, in order.
    internal sealed record FragmentNode(IReadOnlyList<TemplateNode> Nodes) : TemplateNode;
}
