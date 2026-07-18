using System.Globalization;

using NgSharp;

namespace NgSharp.Tests;

public class NgElementTests
{
    [Fact]
    public void Parse_Reads_A_Decimal_Literal_Culture_Invariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            // A template literal always uses '.' as the decimal separator, regardless of the
            // thread culture. Under fr-FR '.' is the group separator, so a culture-sensitive
            // parse would misread "1.5".
            var element = NgElement.Parse("1.5");

            Assert.Equal(1.5, element.GetDouble());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
