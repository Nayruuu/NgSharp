using System;
using System.Collections.Generic;

using NgSharp;
using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Rendering;

// Bundles the constant registries so they don't have to be threaded one by one. Also holds the
// <ng-template> registry and a recursion-depth counter for @render.
internal sealed class RenderScope
{
    // Per-render scratch for a slow-path element's attribute list. Reentrancy-safe ONLY because
    // WriteElement fully consumes it BEFORE recursing into children; never hand it to a custom
    // [directive] (DirectiveElement stashes the reference as a live view).
    public readonly List<KeyValuePair<string, string>> ScratchAttributes = new List<KeyValuePair<string, string>>();

    // The render-time scope chain: outer contexts at the bottom, current on top. [for] pushes an IMPLICIT
    // frame (bare names resolve against it); @for (p of X) pushes a NAMED frame (only "p" reaches it).
    // The chain itself carries the render's strict flag (see ScopeChain).
    private readonly ScopeChain _scopeChain;

    private int _templateDepth;

    public IReadOnlyDictionary<string, IPipe> Pipes { get; }

    public IReadOnlyDictionary<string, ComponentRegistration> Components { get; }

    public IReadOnlyDictionary<string, IDirective> Directives { get; }

    // Named <ng-template> fragments (see TemplateDefNode), looked up by @render (RenderTemplateNode).
    public IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> Templates { get; }

    // Opt-in render caps — null on the default path (TemplateRenderer.Render normalizes RenderLimits.None
    // to null once, at the head), so every enforcement site is a null-check that is never taken by default.
    public RenderLimits Limits { get; }

    // Deliberately the concrete list type: ResolvePath's per-frame walk must index it without interface dispatch.
    public ScopeChain ScopeChain => _scopeChain;

    public RenderScope(
        IReadOnlyDictionary<string, IPipe> pipes,
        IReadOnlyDictionary<string, ComponentRegistration> components,
        IReadOnlyDictionary<string, IDirective> directives,
        IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates,
        RenderLimits limits = null,
        bool strict = false)
    {
        Pipes = pipes;
        Components = components;
        Directives = directives;
        Templates = templates;
        Limits = limits;
        _scopeChain = new ScopeChain(strict);
    }

    public void EnterScope(NgElement context, string name = null) => _scopeChain.Add(new ScopeFrame(context, name));

    // A [for]/@for iteration frame: carries (index, count) so $index/$count/$first/$last can find it.
    public void EnterLoopScope(NgElement context, string name, int index, int count) => _scopeChain.Add(new ScopeFrame(context, name, index, count));

    public void ExitScope() => _scopeChain.RemoveAt(_scopeChain.Count - 1);

    // @render(name, ctx): the fragment sees ONLY the given context; returns the saved chain to restore.
    public ScopeFrame[] EnterIsolatedScope(NgElement context)
    {
        var saved = _scopeChain.ToArray();
        _scopeChain.Clear();
        _scopeChain.Add(new ScopeFrame(context, null));

        return saved;
    }

    public void ExitIsolatedScope(ScopeFrame[] saved)
    {
        _scopeChain.Clear();
        _scopeChain.AddRange(saved);
    }

    // Guards @render against runaway recursion. Default path: false once the built-in cap (50) is
    // reached, so the renderer silently stops instead of overflowing the stack — unchanged behavior.
    // Under opt-in limits, MaxRenderDepth generalizes that cap and exceeding it THROWS instead of
    // truncating (an untrusted template must fail loudly, not ship a silently-shortened document).
    public bool EnterTemplate()
    {
        _templateDepth++;

        if (Limits is null)
        {
            return _templateDepth <= 50;
        }

        if (_templateDepth > Limits.MaxRenderDepth)
        {
            ThrowRenderDepthExceeded(Limits.MaxRenderDepth);
        }

        return true;
    }

    public void ExitTemplate() => _templateDepth--;

    private static void ThrowRenderDepthExceeded(int maxRenderDepth)
        => throw new NgSharpException(
            $"Render limit exceeded: @render nesting is deeper than MaxRenderDepth = {maxRenderDepth}.");
}
