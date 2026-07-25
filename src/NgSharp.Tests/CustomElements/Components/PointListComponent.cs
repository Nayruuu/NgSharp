using System.Globalization;
using System.Text.Json.Serialization;

using NgSharp.Components;

namespace NgSharp.Tests.CustomElements;

// Test component with a complex List<T> property to exercise ConvertValue's complex-type binding.
public class PointListComponent : IComponent
{
    public string ComponentName => "point-list";

    public string? Title { get; set; }

    public List<PointItem>? Points { get; set; }

    public string Render()
    {
        if (Points is null)
        {
            return "<div>no points</div>";
        }

        var items = string.Join("", Points.Select(point =>
            $"<li>{point.Name}{point.Tag}:{point.X.ToString(CultureInfo.InvariantCulture)},{point.Y.ToString(CultureInfo.InvariantCulture)}</li>"));

        return $"<ul data-title=\"{Title}\">{items}</ul>";
    }
}

public class PointItem
{
    public string? Name { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    // Never serialized: visible in the render ONLY when the live CLR instance was bound as-is.
    [JsonIgnore]
    public string? Tag { get; set; }
}
