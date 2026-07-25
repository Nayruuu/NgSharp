using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using SkiaSharp;

namespace NgSharp.Components;

public class MapComponent : IComponent
{
    private const int TILE_SIZE = 256;

    public string ComponentName => "map";

    public int Width { get; set; }

    public int Height { get; set; }

    public string ApiKey { get; set; }

    public byte[] IconData { get; set; }

    public int? Zoom { get; set; }

    public int? IconSize { get; set; }

    public double? SwLatitude { get; set; }

    public double? SwLongitude { get; set; }

    public double? NeLatitude { get; set; }

    public double? NeLongitude { get; set; }

    public IEnumerable<MapPoint> MapPoints { get; set; }

    public string Render()
    {
        var bounds = NeLatitude.HasValue && NeLongitude.HasValue && SwLatitude.HasValue && SwLongitude.HasValue
            ? new BoundEntity
            {
                SW = new CoordinateEntity { Latitude = SwLatitude.Value, Longitude = SwLongitude.Value },
                NE = new CoordinateEntity { Latitude = NeLatitude.Value, Longitude = NeLongitude.Value }
            }
            : new BoundEntity
            {
                SW = new CoordinateEntity { Latitude = MapPoints.Min(x => x.Latitude), Longitude = MapPoints.Min(x => x.Longitude) },
                NE = new CoordinateEntity { Latitude = MapPoints.Max(x => x.Latitude), Longitude = MapPoints.Max(x => x.Longitude) }
            };

        var zoom = Zoom ?? GetMapZoomLevel(bounds, Width, Height);
        var center = GetCenter(bounds);
        var realBounds = GetBounds(center, zoom, Width, Height);

        var markersData = DrawMarkersLayer(MapPoints, realBounds, zoom, Width, Height, IconData);
        var mapUrl = $"https://maps.googleapis.com/maps/api/staticmap?size={Width}x{Height}&center={center}&zoom={zoom}&key={ApiKey}";

        return $"<div class=\"map\" style=\"background:url({mapUrl}); height:{Height}px; width:{Width}px;\"><img src=\"{markersData}\"></div>";
    }

    private int GetMapZoomLevel(BoundEntity bounds, int mapWidth, int mapHeight)
    {
        var zoomMax = 21;
        var worldDim = 256;

        double LatRad(double lat)
        {
            var sin = Math.Sin(lat * Math.PI / 180);
            var radX2 = Math.Log((1 + sin) / (1 - sin)) / 2;

            return Math.Max(Math.Min(radX2, Math.PI), -Math.PI) / 2;
        }

        int ZoomFor(int mapPx, int worldPx, double fraction)
        {
            return (int)(Math.Log(mapPx / worldPx / fraction) / Math.Log(2));
        }

        var latFraction = (LatRad(bounds.NE.Latitude) - LatRad(bounds.SW.Latitude)) / Math.PI;

        var lngDiff = bounds.NE.Longitude - bounds.SW.Longitude;
        var lngFraction = ((lngDiff < 0) ? (lngDiff + 360) : lngDiff) / 360;

        var lngZoom = bounds.NE.Longitude == bounds.SW.Longitude ? zoomMax : ZoomFor(mapWidth, worldDim, lngFraction);
        var latZoom = bounds.NE.Latitude == bounds.SW.Latitude ? zoomMax : ZoomFor(mapHeight, worldDim, latFraction);

        return Math.Min(Math.Min(latZoom, lngZoom), zoomMax);
    }

    private static CoordinateEntity GetCenter(BoundEntity bounds)
    {
        return new CoordinateEntity
        {
            Latitude = (bounds.NE.Latitude + bounds.SW.Latitude) / 2,
            Longitude = (bounds.NE.Longitude + bounds.SW.Longitude) / 2
        };
    }

    private CoordinateEntity GetMercator(double latitude, double longitude, int tileSizeX, int tileSizeY)
    {
        double Bound(double value, double min, double max)
        {
            value = Math.Min(value, max);

            return Math.Max(value, min);
        }

        var sinY = Bound(Math.Sin(latitude * Math.PI / 180), -.9999, .9999);

        return new CoordinateEntity
        {
            Longitude = tileSizeX * (0.5 + longitude / 360),
            Latitude = tileSizeY * (0.5 - Math.Log((1 + sinY) / (1 - sinY)) / (4 * Math.PI))
        };
    }

    private BoundEntity GetBounds(CoordinateEntity center, int zoom, int mapWidth, int mapHeight)
    {
        CoordinateEntity InverseMercator(double latitude, double longitude)
        {
            return new CoordinateEntity
            {
                Longitude = (longitude * 360) / TILE_SIZE - 180,
                Latitude = 360 * Math.Atan(Math.Exp((0.5 - latitude / TILE_SIZE) * (2 * Math.PI))) / Math.PI - 90
            };
        }

        var scale = Math.Pow(2, zoom);
        var centerWorld = GetMercator(center.Latitude, center.Longitude, TILE_SIZE, TILE_SIZE);
        var centerPixel = new CoordinateEntity { Latitude = centerWorld.Latitude * scale, Longitude = centerWorld.Longitude * scale };

        var nePixel = new CoordinateEntity { Latitude = centerPixel.Latitude - mapHeight / 2.0, Longitude = centerPixel.Longitude + mapWidth / 2.0 };
        var swPixel = new CoordinateEntity { Latitude = centerPixel.Latitude + mapHeight / 2.0, Longitude = centerPixel.Longitude - mapWidth / 2.0 };

        var neWorld = new CoordinateEntity { Latitude = nePixel.Latitude / scale, Longitude = nePixel.Longitude / scale };
        var swWorld = new CoordinateEntity { Latitude = swPixel.Latitude / scale, Longitude = swPixel.Longitude / scale };

        var neLatLon = InverseMercator(neWorld.Latitude, neWorld.Longitude);
        var swLatLon = InverseMercator(swWorld.Latitude, swWorld.Longitude);

        return new BoundEntity { NE = neLatLon, SW = swLatLon };
    }

    private SKBitmap GetMarkerIcon(byte[] markerIconData)
    {
        using var memoryStream = new SKMemoryStream(markerIconData);

        return SKBitmap.Decode(memoryStream);
    }

    private void DrawMarker(SKCanvas markersLayer, BoundEntity bounds, MapPoint point, SKBitmap markerIcon, int zoom)
    {
        var scale = Math.Pow(2, zoom);
        var iconSize = markerIcon.Height;
        var targetSize = IconSize ?? iconSize;
        var realOrientation = 0.0d;

        if (markerIcon.Height < markerIcon.Width && point.Orientation.HasValue)
        {
            realOrientation = ((180 - point.Orientation.Value) + 360) % 360;
            realOrientation = (Math.Round(realOrientation / 10) % 36) * iconSize;
        }

        var sourceRect = new SKRectI((int)realOrientation, 0, (int)realOrientation + iconSize, iconSize);
        var croppedIcon = new SKBitmap(iconSize, iconSize);

        using (var surface = new SKCanvas(croppedIcon))
        {
            surface.DrawBitmap(markerIcon, sourceRect, new SKRect(0, 0, targetSize, targetSize));

            var ne = GetMercator(bounds.NE.Latitude, bounds.NE.Longitude, TILE_SIZE, TILE_SIZE);
            var sw = GetMercator(bounds.SW.Latitude, bounds.SW.Longitude, TILE_SIZE, TILE_SIZE);
            var newPoint = GetMercator(point.Latitude, point.Longitude, TILE_SIZE, TILE_SIZE);

            var x = (newPoint.Longitude - sw.Longitude) * scale - targetSize / 2;
            var y = (newPoint.Latitude - ne.Latitude) * scale - targetSize;

            markersLayer.DrawBitmap(croppedIcon, (float)x, (float)y);
        }
    }

    private string DrawMarkersLayer(IEnumerable<MapPoint> points, BoundEntity bounds, int zoom, int mapWidth, int mapHeight, byte[] markerIconData)
    {
        using var imageStream = new MemoryStream();
        using var globalMarkerIcon = GetMarkerIcon(markerIconData);
        using var markersLayerGraphic = SKSurface.Create(new SKImageInfo(mapWidth, mapHeight, SKColorType.Bgra8888, SKAlphaType.Premul));

        var canvas = markersLayerGraphic.Canvas;
        canvas.Clear(SKColors.Transparent);

        foreach (var point in points)
        {
            if (point.IconData is not null)
            {
                using var pointIcon = GetMarkerIcon(point.IconData);

                DrawMarker(canvas, bounds, point, pointIcon, zoom);
            }
            else
            {
                DrawMarker(canvas, bounds, point, globalMarkerIcon, zoom);
            }
        }

        using var image = markersLayerGraphic.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        encoded.SaveTo(imageStream);

        return $"data:image/png;base64,{Convert.ToBase64String(imageStream.ToArray())}";
    }
}
