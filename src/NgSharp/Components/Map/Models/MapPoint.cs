namespace NgSharp.Components
{
    public class MapPoint
    {
        public double Latitude { get; set; }
        
        public double Longitude { get; set; }
        
        public double? Orientation { get; set; }

        private MapPoint()
        {
            
        }

        public MapPoint(double latitude, double longitude, double? orientation = null)
        {
            this.Latitude = latitude;
            this.Longitude = longitude;
            this.Orientation = orientation;
        }
    }
}