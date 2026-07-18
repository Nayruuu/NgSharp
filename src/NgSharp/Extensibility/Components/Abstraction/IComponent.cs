namespace NgSharp.Components
{
    /// <summary>
    /// A server-side component rendered in templates as a custom element
    /// <c>&lt;component-name [prop]="expr"&gt;&lt;/component-name&gt;</c>. Register it on a builder with
    /// <see cref="HtmlBuilder.RegisterComponent{T}"/>.
    /// </summary>
    public interface IComponent
    {
        /// <summary>
        /// The custom element name the component renders for (e.g. <c>"user-card"</c>).
        /// </summary>
        string ComponentName { get; }

        /// <summary>
        /// Returns the HTML that replaces the <c>&lt;component-name&gt;</c> element. The instance's
        /// writable properties are bound from the element's <c>[prop]</c> attributes before this is called.
        /// </summary>
        /// <returns>The component's rendered HTML.</returns>
        string Render();
    }
}
