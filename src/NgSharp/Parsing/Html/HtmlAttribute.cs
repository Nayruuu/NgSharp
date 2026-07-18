namespace NgSharp.Parsing
{
    internal readonly struct HtmlAttribute
    {
        public HtmlAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        // "" for a valueless (boolean) attribute — matches AngleSharp's disabled -> disabled="".
        public string Value { get; }
    }
}
