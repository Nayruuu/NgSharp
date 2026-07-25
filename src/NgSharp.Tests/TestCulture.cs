using System.Globalization;
using System.Runtime.CompilerServices;

namespace NgSharp.Tests;

// Pins the whole test run to InvariantCulture so culture-sensitive output (e.g. a number pipe's
// decimal separator — "1234.5" vs "1234,5") is deterministic regardless of the machine's locale.
// The engine itself stays locale-aware; only the tests fix a culture so goldens are portable
// (a French dev box and an invariant-culture CI runner must agree).
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
