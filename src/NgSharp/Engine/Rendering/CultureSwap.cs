using System;
using System.Globalization;

namespace NgSharp.Rendering;

// The per-render culture swap: save -> set -> restore on Dispose (the using compiles to try/finally).
// CurrentCulture/CurrentUICulture are thread-local, so concurrent renders on other threads are
// unaffected. A null culture is a no-op — the render keeps the ambient culture.
internal readonly struct CultureSwap : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUICulture;

    public CultureSwap(CultureInfo culture)
    {
        if (culture is null)
        {
            _previousCulture = null;
            _previousUICulture = null;

            return;
        }

        _previousCulture = CultureInfo.CurrentCulture;
        _previousUICulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        if (_previousCulture is null)
        {
            return;
        }

        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUICulture;
    }
}
