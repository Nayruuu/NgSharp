namespace NgSharp.Pipes
{
    /// <summary>
    /// The shape the <see cref="ImagePipe"/> reads: a file name (for the MIME type) and its bytes,
    /// rendered as a base64 data URI.
    /// </summary>
    public class ImageData
    {
        /// <summary>
        /// The file name; its extension determines the image MIME type (e.g. <c>"logo.png"</c>).
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The raw file bytes (serialized as base64 by System.Text.Json).
        /// </summary>
        public byte[] FileContent { get; set; }
    }
}
