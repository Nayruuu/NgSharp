#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Polyfill for records / init-only setters on netstandard2.1; net5.0+ provides it, hence the exclusion.
    internal static class IsExternalInit
    {
    }
}
#endif
