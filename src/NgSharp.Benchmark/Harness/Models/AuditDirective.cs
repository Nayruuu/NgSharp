using NgSharp.Directives;

namespace NgSharp.Benchmark;

// A custom directive ([audit]="expr"): flags the host element as audit-required when the expression
// is truthy — the showcase counterpart of the test suite's HiddenDirective pattern.
public sealed class AuditDirective : IDirective
{
    public string DirectiveName => "audit";

    public void Apply(DirectiveElement element, NgElement content)
    {
        if (content.GetBoolean() == true)
        {
            element.SetAttribute("data-audit", "required");
        }
    }
}
