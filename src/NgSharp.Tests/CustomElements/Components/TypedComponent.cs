using NgSharp.Components;

namespace NgSharp.Tests.CustomElements
{
    // Test component with a byte[] property to exercise ConvertValue's base64 decoding.
    public class TypedComponent : IComponent
    {
        public string ComponentName => "typed";

        public byte[] Payload { get; set; }

        public string Render()
        {
            return $"<div>{Payload?.Length ?? -1}</div>";
        }
    }
}
