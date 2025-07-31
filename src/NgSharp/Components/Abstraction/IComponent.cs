using AngleSharp.Dom;

namespace NgSharp.Components
{
    public interface IComponent
    {
        public string ComponentName { get; }

        public void Render(IElement element);
    }
}

