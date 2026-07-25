using System.Linq;
using NgSharp.Parsing;

namespace NgSharp.Tests.Expressions;

public class ExpressionLexerTests
{
    [Fact]
    public void Tokenizes_A_Numeric_Comparison()
    {
        var tokens = ExpressionLexer.Tokenize("Count == 3")
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
        var tokens = ExpressionLexer.Tokenize("Amount | number: 'N0'")
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
        var tokens = ExpressionLexer.Tokenize("Ok == true ? 'a' : 'b'")
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

    [Fact]
    public void Tokenizes_Unary_Not()
    {
        var tokens = ExpressionLexer.Tokenize("!Flag")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Not, "!"),
                (TokenKind.Identifier, "Flag")
            },
            tokens);
    }

    [Fact]
    public void Distinguishes_Unary_Not_From_Not_Equals()
    {
        var tokens = ExpressionLexer.Tokenize("!A != B")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Not, "!"),
                (TokenKind.Identifier, "A"),
                (TokenKind.Operator, "!="),
                (TokenKind.Identifier, "B")
            },
            tokens);
    }

    [Fact]
    public void Folds_Safe_Navigation_Into_A_Dotted_Path()
    {
        var tokens = ExpressionLexer.Tokenize("User?.Address?.City")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Identifier, "User.Address.City")
            },
            tokens);
    }

    [Fact]
    public void Keeps_The_Ternary_Question_Separate_From_Safe_Nav()
    {
        var tokens = ExpressionLexer.Tokenize("A?.B ? C : D")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[]
            {
                (TokenKind.Identifier, "A.B"),
                (TokenKind.Question, "?"),
                (TokenKind.Identifier, "C"),
                (TokenKind.Colon, ":"),
                (TokenKind.Identifier, "D")
            },
            tokens);
    }

    [Fact]
    public void Folds_An_Array_Index_Into_The_Path()
    {
        var tokens = ExpressionLexer.Tokenize("Users[0].Name")
            .Where(t => t.Kind != TokenKind.End)
            .Select(t => (t.Kind, t.Text))
            .ToArray();

        Assert.Equal(
            new[] { (TokenKind.Identifier, "Users[0].Name") },
            tokens);
    }
}
