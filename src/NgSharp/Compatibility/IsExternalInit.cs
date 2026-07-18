#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Polyfill so records / init-only setters compile on netstandard2.1, which lacks this BCL type.
    // net5.0+ (our net8.0 target) provides it in the framework, so it is excluded there.
    internal static class IsExternalInit
    {
    }
}
#endif
