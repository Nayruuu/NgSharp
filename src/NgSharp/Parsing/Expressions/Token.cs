namespace NgSharp.Parsing
{
    internal readonly struct Token
    {
        public TokenKind Kind { get; }

        public string Text { get; }

        public int Position { get; }

        public Token(TokenKind kind, string text, int position)
        {
            Kind = kind;
            Text = text;
            Position = position;
        }

        public override string ToString() => $"{Kind}:'{Text}'";
    }
}
