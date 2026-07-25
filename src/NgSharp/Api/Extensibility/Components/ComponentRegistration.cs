using System;
using System.Diagnostics.CodeAnalysis;

namespace NgSharp.Components;

// Registry entry for a server component: the shared instance (immutable, thread-safe) plus its type,
// annotated so the trimmer preserves the parameterless constructor and public properties that
// per-render activation and property binding reflect over.
internal sealed class ComponentRegistration
{
    public readonly IComponent Instance;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public readonly Type Type;

    public ComponentRegistration(
        IComponent instance,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        Instance = instance;
        Type = type;
    }
}
