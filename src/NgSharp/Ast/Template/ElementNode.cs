using System.Collections.Generic;

namespace NgSharp.Ast
{
    internal sealed record ElementNode(
        string TagName,
        IReadOnlyList<AttributeNode> Attributes,
        IReadOnlyList<BindingNode> Bindings,
        IReadOnlyList<DirectiveBinding> Directives,
        IReadOnlyList<TemplateNode> Children) : TemplateNode;
}
