using System.Collections.Generic;

namespace NgSharp.Directives
{
    // A mutable, AngleSharp-free view of the element a custom directive is applied to, exposed to
    // IDirective.Apply. Backed by the renderer's live attribute list, so changes flow into the output.
    public sealed class DirectiveElement
    {
        private readonly List<KeyValuePair<string, string>> attributes;

        internal DirectiveElement(string tagName, List<KeyValuePair<string, string>> attributes)
        {
            TagName = tagName;
            this.attributes = attributes;
        }

        public string TagName { get; }

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

        public void RemoveAttribute(string name)
        {
            attributes.RemoveAll(attribute => attribute.Key == name);
        }
    }
}
