namespace NgSharp.Expressions
{
    public enum TokenKind
    {
        Identifier,
        Number,
        String,
        Operator,
        And,
        Or,
        Pipe,
        Question,
        Colon,
        LParen,
        RParen,
        End
    }

    public readonly struct Token
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
