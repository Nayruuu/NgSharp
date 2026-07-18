using System.Text.Json;
using System.Collections.Generic;

namespace NgSharp
{
    // Builds the NgElement tree from a System.Text.Json JsonElement (the reflection-free ingestion path).
    public partial class NgElement
    {
        /// <summary>
        /// Builds an <see cref="NgElement"/> tree from a <see cref="JsonElement"/> — the reflection-free
        /// ingestion path (Native AOT / trimming safe).
        /// </summary>
        /// <remarks>
        /// Only scalar leaves populate <see cref="NgElement.Value"/>; object and array nodes keep it null
        /// (matching <see cref="FromObject(object, NgElement, string)"/>), so a pipe that re-deserializes a
        /// whole object from <c>value.Value</c> won't work here — read its fields with <see cref="NgElement.SelectToken"/>.
        /// </remarks>
        /// <param name="jsonElement">The JSON to convert.</param>
        /// <param name="parent">The parent node (null at the root; supplied during recursion).</param>
        /// <param name="key">This node's key (empty at the root; supplied during recursion).</param>
        /// <returns>The root of the built tree.</returns>
        public static NgElement FromJson(JsonElement jsonElement, NgElement parent = null, string key = "")
        {
            var ng = new NgElement
            {
                Key = key,
                Parent = parent,
                ValueKind = jsonElement.ValueKind
            };

            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    var members = new List<NgElement>();

                    foreach (var prop in jsonElement.EnumerateObject())
                    {
                        members.Add(FromJson(prop.Value, ng, prop.Name));
                    }

                    ng.members = members.ToArray();

                    break;

                case JsonValueKind.Array:
                    ng.children = new List<NgElement>();
                    int i = 0;

                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        ng.children.Add(FromJson(item, ng, $"[{i++}]"));
                    }

                    break;

                default:
                    ng.Value = JsonElementToObject(jsonElement);
                    break;
            }

            return ng;
        }

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l
                    : element.TryGetDouble(out var d) ? d
                    : element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText() // Object, Array, Undefined…
            };
        }
    }
}
