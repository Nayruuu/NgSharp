namespace NgSharp.Components;

/// <summary>
/// A server-side component rendered in templates as a custom element
/// <c>&lt;component-name [prop]="expr"&gt;&lt;/component-name&gt;</c>. Register it on a builder with
/// <see cref="HtmlBuilder.RegisterComponent{T}()"/>.
/// </summary>
/// <remarks>
/// A component's <see cref="Render"/> output is trusted raw HTML: the engine injects it verbatim,
/// without escaping — that is the point of a component (it emits markup), and the same contract as
/// the <c>[html]</c> binding. The flip side: any user-supplied data a component embeds must be
/// escaped by the component itself (e.g. <c>System.Net.WebUtility.HtmlEncode</c>), or it is an XSS
/// vector — <c>&lt;script&gt;</c> in a bound property lands in the page as a live tag.
/// </remarks>
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
    /// <remarks>
    /// The returned string is emitted verbatim — trusted raw HTML, never escaped by the engine.
    /// HTML-encode user data before embedding it (e.g. <c>System.Net.WebUtility.HtmlEncode</c>).
    /// </remarks>
    /// <returns>The component's rendered HTML, injected unescaped.</returns>
    string Render();
}
