using System.Text.Json;

using NgSharp.Components;

namespace NgSharp.Tests.Extensibility;

// End-to-end <map> render: MapPoints (a complex IEnumerable<MapPoint>) binds through ConvertValue's
// complex-type path, then the component draws the markers layer with SkiaSharp.
public class MapComponentRenderTests
{
    // IconSize is bound too: without it the 512px-native icon dwarfs the 400×300 canvas, and the
    // nullable-int property exercises ConvertValue's scalar path alongside the complex-type one.
    private const string Template =
        "<map [Width]=\"Width\" [Height]=\"Height\" [ApiKey]=\"ApiKey\" [IconData]=\"Icon\" [IconSize]=\"IconSize\" [MapPoints]=\"MapPoints\"></map>";

    [SkippableFact]
    public void Map_Renders_Markers_From_Clr_Model()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<MapComponent>();

        var model = new
        {
            Width = 400,
            Height = 300,
            ApiKey = "TestKey",
            Icon = File.ReadAllBytes("Templates/big-test-marker-icon.webp"),
            IconSize = 28,
            MapPoints = new List<MapPoint>
            {
                new(48.8566, 2.3522),
                new(48.8600, 2.3419),
                new(48.8530, 2.3499)
            }
        };

        var content = RenderOrSkipWithoutSkiaNative(() => builder.BuildFromTemplate(Template, model));

        Assert.Contains("https://maps.googleapis.com/maps/api/staticmap?size=400x300", content);
        Assert.Contains("zoom=", content);
        Assert.Contains("key=TestKey", content);
        Assert.Contains("<img src=\"data:image/png;base64,", content);
        AssertMarkersLayerReallyDrawn(content, 400, 300);
    }

    [SkippableFact]
    public void Map_Renders_Markers_From_Json_Model()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<MapComponent>();

        // byte[] → base64 string and MapPoint's public constructor both round-trip through STJ.
        var json = JsonSerializer.SerializeToElement(new
        {
            Width = 400,
            Height = 300,
            ApiKey = "TestKey",
            Icon = File.ReadAllBytes("Templates/big-test-marker-icon.webp"),
            IconSize = 28,
            MapPoints = new List<MapPoint>
            {
                new(48.8566, 2.3522),
                new(48.8600, 2.3419, 90)
            }
        });

        var content = RenderOrSkipWithoutSkiaNative(() => builder.BuildFromTemplate(Template, json));

        Assert.Contains("https://maps.googleapis.com/maps/api/staticmap?size=400x300", content);
        Assert.Contains("<img src=\"data:image/png;base64,", content);
        AssertMarkersLayerReallyDrawn(content, 400, 300);
    }

    // Decodes the emitted base64 PNG and proves the markers layer was REALLY drawn: exact canvas size
    // and a meaningful number of non-transparent pixels (a blank/garbage layer would pass the string asserts).
    private static void AssertMarkersLayerReallyDrawn(string content, int expectedWidth, int expectedHeight)
    {
        var start = content.IndexOf("data:image/png;base64,", StringComparison.Ordinal) + "data:image/png;base64,".Length;
        var end = content.IndexOf('"', start);
        var pngBytes = Convert.FromBase64String(content[start..end]);

        using var bitmap = SkiaSharp.SKBitmap.Decode(pngBytes);

        Assert.NotNull(bitmap);
        Assert.Equal(expectedWidth, bitmap.Width);
        Assert.Equal(expectedHeight, bitmap.Height);

        var drawnPixels = 0;
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    drawnPixels++;
                }
            }
        }

        Assert.True(drawnPixels > 100, $"markers layer contains only {drawnPixels} drawn pixels — nothing was rendered onto the canvas");
    }

    private static string RenderOrSkipWithoutSkiaNative(Func<string> render)
    {
        try
        {
            return render();
        }
        catch (Exception exception) when (IsNativeLoadFailure(exception))
        {
            throw new SkipException($"SkiaSharp native library failed to load in the test runner: {exception.Message}");
        }
    }

    private static bool IsNativeLoadFailure(Exception exception) =>
        exception is DllNotFoundException
        || exception is TypeInitializationException
        || (exception.InnerException is not null && IsNativeLoadFailure(exception.InnerException));
}
