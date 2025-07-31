using AngleSharp.Dom;
using NgSharp.Directives;

namespace NgSharp.Tests.CustomElements;

public class HiddenDirective : IDirective
{
    public string DirectiveName => "hidden";
    public bool ApplyWhileParsing => false;

    public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
    {
        var booleanValue = content.GetBoolean();

        if (booleanValue == true)
        {
            childElement.SetAttribute("hidden", string.Empty);
        }
    }
}