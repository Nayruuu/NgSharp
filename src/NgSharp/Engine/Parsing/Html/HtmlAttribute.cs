namespace NgSharp.Parsing;

internal readonly struct HtmlAttribute
{
    public string Name { get; }

    // "" for a valueless (boolean) attribute (disabled -> disabled="").
    public string Value { get; }

    public HtmlAttribute(string name, string value)
    {
        Name = name;
        Value = value;
    }
}
