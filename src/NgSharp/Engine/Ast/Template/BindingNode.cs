namespace NgSharp.Ast;

internal sealed record BindingNode(BindingKind Kind, string Target, Expression Expression);
