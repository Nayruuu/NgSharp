using System.Linq;
using NgSharp.Expressions;

namespace NgSharp.Tests.Expressions;

public class LexerTests
{
    [Fact]
    public void Tokenizes_A_Numeric_Comparison()
    {
        var tokens = Lexer.Tokenize("Count == 3")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Identifier, "Count"),
                (TokenKind.Operator, "=="),
                (TokenKind.Number, "3")
            },
            tokens);
    }

    [Fact]
    public void Tokenizes_A_Pipe_With_A_String_Argument()
    {
        var tokens = Lexer.Tokenize("Amount | number: 'N0'")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Identifier, "Amount"),
                (TokenKind.Pipe, "|"),
                (TokenKind.Identifier, "number"),
                (TokenKind.Colon, ":"),
                (TokenKind.String, "N0")
            },
            tokens);
    }

    [Fact]
    public void Tokenizes_A_Ternary()
    {
        var tokens = Lexer.Tokenize("Ok == true ? 'a' : 'b'")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Identifier, "Ok"),
                (TokenKind.Operator, "=="),
                (TokenKind.Identifier, "true"),
                (TokenKind.Question, "?"),
                (TokenKind.String, "a"),
                (TokenKind.Colon, ":"),
                (TokenKind.String, "b")
            },
            tokens);
    }
}
