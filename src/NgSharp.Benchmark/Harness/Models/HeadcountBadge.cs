using NgSharp.Components;

namespace NgSharp.Benchmark;

// A custom component (server-rendered HTML fragment). Property set from [total]="..." on the tag.
public sealed class HeadcountBadge : IComponent
{
    public string ComponentName => "headcount-badge";

    public int Total { get; set; }

    public string Render() => $"<footer class=\"headcount\">Total headcount: {Total}</footer>";
}
