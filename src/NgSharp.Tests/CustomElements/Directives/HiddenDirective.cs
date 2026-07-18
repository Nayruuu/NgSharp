using NgSharp.Directives;

namespace NgSharp.Tests.CustomElements;

public class HiddenDirective : IDirective
{
    public string DirectiveName => "hidden";

    public void Apply(DirectiveElement element, NgElement content)
    {
        if (content.GetBoolean() == true)
        {
            element.SetAttribute("hidden", string.Empty);
        }
    }
}
