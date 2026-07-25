using System.Collections.Generic;

namespace NgSharp.Rendering;

// The concrete render-time scope chain, extended with the render's strict flag. Deriving from List
// keeps ResolvePath's per-frame walk on the devirtualized List indexer AND carries strictness to the
// exact spot that needs it without threading a parameter through the evaluator's hot signatures.
// Strict is readonly and read ONLY inside resolution-failure branches — the happy path never tests it.
internal sealed class ScopeChain : List<ScopeFrame>
{
    public readonly bool Strict;

    public ScopeChain(bool strict)
    {
        Strict = strict;
    }

    public ScopeChain(int capacity, bool strict)
        : base(capacity)
    {
        Strict = strict;
    }
}
