using System.IO;
using System.Text.Json;

namespace NgSharp.Pipes
{
    public class ImageData
    {
        public string FileName { get; set; }

        public byte[] FileContent { get; set; }
    }

    public class ImagePipe : IPipe
    {
        public string PipeName => "image";

        // Reads the FileName/FileContent sub-fields through the NgElement tree instead of
        // re-deserializing the whole object from value.Value — so it works whether the model tree
        // was built from JSON (FromJson) or straight from the object (FromObject). FileContent is
        // already a base64 string in both paths.
        public string Transform(string tagName, NgElement value, string argument)
        {
            if (value == null || value.ValueKind == JsonValueKind.Null)
            {
                return string.Empty;
            }

            var base64 = value.SelectToken("FileContent")?.GetString();

            if (string.IsNullOrEmpty(base64))
            {
                return string.Empty;
            }

            var fileName = value.SelectToken("FileName")?.GetString();
            var extension = Path.GetExtension(fileName ?? string.Empty).Replace(".", "");

            if (!string.IsNullOrEmpty(tagName) && tagName.ToLowerInvariant() == "img")
            {
                return $"data:image/{extension};base64,{base64}";
            }

            return $"url(data:image/{extension};base64,{base64})";
        }
    }
}
