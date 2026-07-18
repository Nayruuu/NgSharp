using System.Collections.Generic;

namespace NgSharp.Directives
{
    /// <summary>
    /// A mutable view of the element a custom directive is applied to, passed to
    /// <see cref="IDirective.Apply"/>. Backed by the renderer's live attribute list, so changes made
    /// here flow into the rendered output.
    /// </summary>
    public sealed class DirectiveElement
    {
        private readonly List<KeyValuePair<string, string>> attributes;

        internal DirectiveElement(string tagName, List<KeyValuePair<string, string>> attributes)
        {
            TagName = tagName;
            this.attributes = attributes;
        }

        /// <summary>
        /// The lowercased tag name of the host element (e.g. <c>"div"</c>).
        /// </summary>
        public string TagName { get; }

        /// <summary>
        /// Reads the current value of an attribute.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The attribute value, or null when the element has no such attribute.</returns>
        public string GetAttribute(string name)
        {
            foreach (var attribute in attributes)
            {
                if (attribute.Key == name)
                {
                    return attribute.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets an attribute, replacing any existing value or adding it when absent.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The attribute value (written verbatim into the output).</param>
        public void SetAttribute(string name, string value)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].Key == name)
                {
                    attributes[i] = new KeyValuePair<string, string>(name, value);
                    return;
                }
            }

            attributes.Add(new KeyValuePair<string, string>(name, value));
        }

        /// <summary>
        /// Removes an attribute; a no-op when the element has no such attribute.
        /// </summary>
        /// <param name="name">The attribute name to remove.</param>
        public void RemoveAttribute(string name)
        {
            attributes.RemoveAll(attribute => attribute.Key == name);
        }
    }
}
