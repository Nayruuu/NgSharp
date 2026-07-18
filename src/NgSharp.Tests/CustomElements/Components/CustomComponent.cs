namespace NgSharp.Components
{
    public class CustomComponent : IComponent
    {
        public string ComponentName => "custom-component";

        public string ComponentText { get; set; }

        public string Render()
        {
            return $"<div>{ComponentText}</div>";
        }
    }
}
