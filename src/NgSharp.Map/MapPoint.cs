namespace NgSharp.Components;

public class MapPoint
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? Orientation { get; set; }

    public byte[] IconData { get; set; }

    public MapPoint(double latitude, double longitude, double? orientation = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        Orientation = orientation;
    }

    private MapPoint()
    {
    }
}
