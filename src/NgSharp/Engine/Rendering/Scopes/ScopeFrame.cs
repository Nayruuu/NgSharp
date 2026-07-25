namespace NgSharp.Rendering;

// A frame in the render-time scope chain. A named frame (@for (p of X)) is reachable ONLY by its name;
// an unnamed frame participates in implicit bare-name resolution. A loop frame (Index >= 0) additionally
// carries the iteration position that $index/$count/$first/$last resolve against.
internal readonly struct ScopeFrame
{
    public readonly NgElement Context;
    public readonly string Name;
    public readonly int Index;
    public readonly int Count;

    public ScopeFrame(NgElement context, string name)
    {
        Context = context;
        Name = name;
        Index = -1;
        Count = 0;
    }

    public ScopeFrame(NgElement context, string name, int index, int count)
    {
        Context = context;
        Name = name;
        Index = index;
        Count = count;
    }
}
