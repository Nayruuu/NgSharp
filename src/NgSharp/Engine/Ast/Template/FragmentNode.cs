using System.Collections.Generic;

namespace NgSharp.Ast;

// A compiled control-flow body (the ng-container wrapper flattened away); rendered as its children in order.
internal sealed record FragmentNode(IReadOnlyList<TemplateNode> Nodes) : TemplateNode;
