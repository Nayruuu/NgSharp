using System.Collections.Generic;

namespace NgSharp.Ast;

// @switch: Value is evaluated ONCE; the FIRST case whose expression equals it (the '==' operator's
// equality, ExpressionEvaluator.AreEqual) renders its body; otherwise Default (null = no @default,
// the switch renders nothing).
internal sealed record SwitchNode(Expression Value, IReadOnlyList<SwitchCase> Cases, TemplateNode Default) : TemplateNode;

// One @case arm: the compared expression and its folded body.
internal sealed record SwitchCase(Expression Value, TemplateNode Body);
