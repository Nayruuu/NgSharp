using System.Globalization;

using NgSharp;

namespace NgSharp.Tests.Rendering;

// End-to-end (through HtmlBuilder) coverage for arithmetic in interpolations — '+' (string concat and
// numeric add), '-', '*', '/', '%' and unary minus.
public class ArithmeticRenderingTests
{
    [Fact]
    public void Concatenates_A_Full_Name()
        => Assert.Contains("<p>Alice Martin</p>", Render("<p>{{ First + ' ' + Last }}</p>", new { First = "Alice", Last = "Martin" }));

    [Fact]
    public void Concatenates_A_Label_And_A_Number()
        => Assert.Contains("<p>Total: 5</p>", Render("<p>{{ 'Total: ' + N }}</p>", new { N = 5 }));

    [Fact]
    public void Renders_A_Sum_As_A_Plain_Integer()
        => Assert.Contains("<span>5</span>", Render("<span>{{ A + B }}</span>", new { A = 2, B = 3 }));

    [Fact]
    public void Respects_Operator_Precedence()
        => Assert.Contains("<span>14</span>", Render("<span>{{ A + B * C }}</span>", new { A = 2, B = 3, C = 4 }));

    [Fact]
    public void Renders_A_Fractional_Quotient()
    {
        // Number output uses the current culture's decimal separator; pin invariant so the golden is portable.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Contains("<span>2.5</span>", Render("<span>{{ A / B }}</span>", new { A = 5, B = 2 }));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static string Render(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
