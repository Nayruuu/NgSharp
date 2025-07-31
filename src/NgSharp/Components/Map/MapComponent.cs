using SkiaSharp;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace NgSharp.Components
{
    internal class CoordinateEntity
    {
        public double Latitude { get; set; }
        
        public double Longitude { get; set; }
        
        public override string ToString()
        {
            return Latitude.ToString().Replace(",", ".") + "," + Longitude.ToString().Replace(",", ".");
        }
    }
    
    internal class BoundEntity
    {
        public CoordinateEntity SW { get; set; }
        
        public CoordinateEntity NE { get; set; }
    }
    
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

        public void Render(IElement element)
        {
            int zoom;
            BoundEntity bounds;

            if (NeLatitude.HasValue && NeLongitude.HasValue && SwLatitude.HasValue && SwLongitude.HasValue)
            {
                bounds = new BoundEntity()
                {
                    SW = new CoordinateEntity() { Latitude = SwLatitude.Value, Longitude = SwLongitude.Value },
                    NE = new CoordinateEntity() { Latitude = NeLatitude.Value, Longitude = NeLongitude.Value }
                };
            }
            else
            {
                bounds = new BoundEntity()
                {
                    SW = new CoordinateEntity() { Latitude = MapPoints.Min(x => x.Latitude), Longitude = MapPoints.Min(x => x.Longitude) },
                    NE = new CoordinateEntity() { Latitude = MapPoints.Max(x => x.Latitude), Longitude = MapPoints.Max(x => x.Longitude) }
                };
            }

            if (Zoom.HasValue)
            {
                zoom = Zoom.Value;
            }
            else
            {
                zoom = GetMapZoomLevel(bounds, Width, Height);
            }

            var center = GetCenter(bounds);
            var realBounds = GetBounds(center, zoom, Width, Height);

            var data = DrawMarkersLayer(MapPoints, realBounds, zoom, Width, Height, IconData);
            var mapUrlParameterized = String.Format("https://maps.googleapis.com/maps/api/staticmap?size={0}x{1}&center={2}&zoom={3}&key={4}",
                Width,
                Height,
                center.ToString(),
                zoom,
                ApiKey);

            var htmlParser = new HtmlParser();

            var image = $"<div class=\"map\" style =\"background:url({mapUrlParameterized}); height:{Height}px; width:{Width}px;\">" +
                        $"  <img src=\"{data}\">" +
                        $"</div>";

            var node = htmlParser.ParseFragment(image, element);

            element.Parent.InsertBefore(node.First(), element);
            element.Parent.RemoveElement(element);
        }

        private int GetMapZoomLevel(BoundEntity bounds, int mapWidth, int mapHeight)
        {
            int zoomMax = 21;
            int worldDim = 256;

            double latRad(double lat)
            {
                var sin = Math.Sin(lat * Math.PI / 180);
                var radX2 = Math.Log((1 + sin) / (1 - sin)) / 2;
                return Math.Max(Math.Min(radX2, Math.PI), -Math.PI) / 2;
            }

            int zoom(int mapPx, int worldPx, double fraction)
            {
                return (int)(Math.Log(mapPx / worldPx / fraction) / Math.Log(2));
            }


            var latFraction = (latRad(bounds.NE.Latitude) - latRad(bounds.SW.Latitude)) / Math.PI;

            var lngDiff = bounds.NE.Longitude - bounds.SW.Longitude;
            var lngFraction = ((lngDiff < 0) ? (lngDiff + 360) : lngDiff) / 360;

            var lngZoom = bounds.NE.Longitude == bounds.SW.Longitude ? zoomMax : zoom(mapWidth, worldDim, lngFraction);
            var latZoom = bounds.NE.Latitude == bounds.SW.Latitude ? zoomMax : zoom(mapHeight, worldDim, latFraction);

            return Math.Min(Math.Min(latZoom, lngZoom), zoomMax);
        }

        private static CoordinateEntity GetCenter(BoundEntity bounds)
        {
            return new CoordinateEntity()
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

            double siny = Bound(Math.Sin(latitude * Math.PI / 180), -.9999, .9999);

            CoordinateEntity c = new CoordinateEntity()
            {
                Longitude = tileSizeX * (0.5 + longitude / 360),
                Latitude = tileSizeY * (0.5 - Math.Log((1 + siny) / (1 - siny)) / (4 * Math.PI))
            };

            return c;
        }

        private BoundEntity GetBounds(CoordinateEntity center, int zoom, int mapWidth, int mapHeight)
        {
            CoordinateEntity InverseMercator(double latitude, double longitude)
            {
                CoordinateEntity c = new CoordinateEntity()
                {
                    Longitude = (longitude * 360) / TILE_SIZE - 180,
                    Latitude = 360 * Math.Atan(Math.Exp((0.5 - latitude / TILE_SIZE) * (2 * Math.PI))) / Math.PI - 90
                };

                return c;
            }

            var scale = Math.Pow(2, zoom);

            var centerWorld = GetMercator(center.Latitude, center.Longitude, TILE_SIZE, TILE_SIZE);
            var test = InverseMercator(centerWorld.Latitude, centerWorld.Longitude);

            var centerPixel = new CoordinateEntity() { Latitude = centerWorld.Latitude * scale, Longitude = centerWorld.Longitude * scale };

            var NEPixel = new CoordinateEntity() { Latitude = centerPixel.Latitude - mapHeight / 2.0, Longitude = centerPixel.Longitude + mapWidth / 2.0 };
            var SWPixel = new CoordinateEntity() { Latitude = centerPixel.Latitude + mapHeight / 2.0, Longitude = centerPixel.Longitude - mapWidth / 2.0 };

            var NEWorld = new CoordinateEntity() { Latitude = NEPixel.Latitude / scale, Longitude = NEPixel.Longitude / scale };
            var SWWorld = new CoordinateEntity() { Latitude = SWPixel.Latitude / scale, Longitude = SWPixel.Longitude / scale };

            var NELatLon = InverseMercator(NEWorld.Latitude, NEWorld.Longitude);
            var SWLatLon = InverseMercator(SWWorld.Latitude, SWWorld.Longitude);

            return new BoundEntity() { NE = NELatLon, SW = SWLatLon };
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

                var NE = GetMercator(bounds.NE.Latitude, bounds.NE.Longitude, TILE_SIZE, TILE_SIZE);
                var SW = GetMercator(bounds.SW.Latitude, bounds.SW.Longitude, TILE_SIZE, TILE_SIZE);

                var newPoint = GetMercator(point.Latitude, point.Longitude, TILE_SIZE, TILE_SIZE);

                var x = (newPoint.Longitude - SW.Longitude) * scale - targetSize / 2;
                var y = (newPoint.Latitude - NE.Latitude) * scale - targetSize;

                markersLayer.DrawBitmap(croppedIcon, (float)x, (float)y);
            }
        }

        private string DrawMarkersLayer(IEnumerable<MapPoint> points, BoundEntity bounds, int zoom, int mapWidth, int mapHeight, byte[] markerIconData)
        {
            using (var imageStream = new MemoryStream())
            {
                var markerIcon = GetMarkerIcon(markerIconData);
                using (var markersLayerGraphic = SKSurface.Create(new SKImageInfo(mapWidth, mapHeight, SKColorType.Bgra8888, SKAlphaType.Premul)))
                {
                    var canvas = markersLayerGraphic.Canvas;
                    canvas.Clear(SKColors.Transparent);

                    foreach (var point in points)
                    {
                        DrawMarker(canvas, bounds, point, markerIcon, zoom);
                    }

                    using var image = markersLayerGraphic.Snapshot();
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    data.SaveTo(imageStream);

                    return $"data:image/png;base64,{System.Convert.ToBase64String(imageStream.ToArray())}";
                }
            }
        }
    }
}

