using System.Collections.Generic;

namespace NgSharp.Ast;

// A named, reusable template fragment defined with <ng-template #name>…</ng-template>. It renders
// nothing where it is written — its body is instantiated on demand by a RenderTemplateNode (@render).
internal sealed record TemplateDefNode(string Name, IReadOnlyList<TemplateNode> Body) : TemplateNode;
