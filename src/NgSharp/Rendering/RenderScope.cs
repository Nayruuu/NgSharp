using System.Collections.Generic;

using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Rendering
{
    // Bundles the constant registries so they don't have to be threaded one by one.
    internal sealed class RenderScope
    {
        public IReadOnlyDictionary<string, IPipe> Pipes { get; }

        public IReadOnlyDictionary<string, IComponent> Components { get; }

        public IReadOnlyDictionary<string, IDirective> Directives { get; }

        public RenderScope(IReadOnlyDictionary<string, IPipe> pipes, IReadOnlyDictionary<string, IComponent> components, IReadOnlyDictionary<string, IDirective> directives)
        {
            Pipes = pipes;
            Components = components;
            Directives = directives;
        }
    }
}
