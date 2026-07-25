using System.Globalization;

using NgSharp.Components;

namespace NgSharp.Tests.CustomElements;

// Test component with DateTimeOffset / TimeSpan properties: deferred scalar boxes carry String kind,
// so they must bind through ConvertValue's raw-carrier check, not the hosted-CLR (Object/Array) path.
public class StampComponent : IComponent
{
    public string ComponentName => "stamp";

    public DateTimeOffset At { get; set; }

    public TimeSpan Window { get; set; }

    public string Render()
        => $"<time>{At.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)}|{Window.ToString("c", CultureInfo.InvariantCulture)}</time>";
}
