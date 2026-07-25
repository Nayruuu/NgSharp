using System.Collections.Generic;

namespace NgSharp.Ast;

internal sealed record ComponentNode(string Name, IReadOnlyDictionary<string, Expression> Properties) : TemplateNode;
