namespace NgSharp.Ast;

// Var is the explicit loop-variable name from @for (Var of Collection); null for a classic implicit [for].
internal sealed record ForNode(Expression Collection, string Var, TemplateNode Body) : TemplateNode;
