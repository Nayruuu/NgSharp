namespace NgSharp.Parsing;

internal readonly struct Token
{
    public TokenKind Kind { get; }

    public string Text { get; }

    // True only for an Identifier that used optional chaining ('a?.b'): the '?' is normalized away in
    // Text, but strict rendering must still see that the author explicitly guarded the path.
    public bool Guarded { get; }

    public Token(TokenKind kind, string text)
        : this(kind, text, guarded: false)
    {
    }

    public Token(TokenKind kind, string text, bool guarded)
    {
        Kind = kind;
        Text = text;
        Guarded = guarded;
    }
}
