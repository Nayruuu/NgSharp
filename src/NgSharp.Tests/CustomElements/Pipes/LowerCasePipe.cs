using AngleSharp.Dom;
using NgSharp.Pipes;

namespace NgSharp.Tests.CustomElements;

public class LowerCasePipe : IPipe
{
    public string PipeName => "lower";
    public string Transform(IElement childElement, NgElement value, string argument)
    {
        return value.GetString()?.ToLower();
    }
}