namespace NgSharp.Components
{
    public interface IComponent
    {
        string ComponentName { get; }

        // Returns the HTML that replaces the <component-name> element. The instance's properties are
        // bound from the element's [prop] attributes before this is called.
        string Render();
    }
}
