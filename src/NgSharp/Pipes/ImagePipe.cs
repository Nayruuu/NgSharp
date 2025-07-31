using System;
using AngleSharp.Dom;

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

        public string Transform(IElement childElement, NgElement value, string argument)
        {
            if (value.ValueKind != JsonValueKind.Null)
            {
                var image = JsonSerializer.Deserialize<ImageData>(value.Value.ToString());

                if (image != null)
                {
                    var imageContent = Convert.ToBase64String(image.FileContent);

                    return
                        $"url(data:image/{Path.GetExtension(image.FileName).Replace(".", "")};base64,{imageContent})";
                }
            }
            
            return string.Empty;
        }
    }
}
