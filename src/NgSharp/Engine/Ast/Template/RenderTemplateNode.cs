namespace NgSharp.Ast;

// Instantiates a named <ng-template #Name> via @render(Name[, contextExpr]). A null Context means
// "the current context".
internal sealed record RenderTemplateNode(string Name, Expression Context) : TemplateNode;
