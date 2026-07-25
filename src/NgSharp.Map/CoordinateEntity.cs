namespace NgSharp.Components;

internal class CoordinateEntity
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public override string ToString()
    {
        return Latitude.ToString().Replace(",", ".") + "," + Longitude.ToString().Replace(",", ".");
    }
}
