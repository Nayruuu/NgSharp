
using NgSharp;
using NgSharp.Pipes;

namespace NgSharp.Tests.Rendering;

// Pipe arguments go through the full expression grammar (unary minus included), so a negative numeric
// argument survives.
public class PipeArgumentTests
{
    private sealed class ArgPipe : IPipe
    {
        public string PipeName => "arg";

        public string Transform(string tagName, NgElement value, string argument) => argument ?? "NULL";
    }

    private static string Render(string tpl, object model)
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe<ArgPipe>();

        return builder.BuildFromTemplate(tpl, model);
    }

    [Fact]
    public void Pipe_Receives_A_Negative_Numeric_Argument()
        => Assert.Contains("<p>-5</p>", Render("<p>{{ V | arg:-5 }}</p>", new { V = "x" }));

    [Fact]
    public void Pipe_Still_Receives_A_String_Argument()
        => Assert.Contains("<p>C2</p>", Render("<p>{{ V | arg:'C2' }}</p>", new { V = "x" }));

    [Fact]
    public void Pipe_Still_Receives_A_Positive_Numeric_Argument()
        => Assert.Contains("<p>7</p>", Render("<p>{{ V | arg:7 }}</p>", new { V = "x" }));
}
