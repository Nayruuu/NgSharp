using NgSharp.Pipes;

namespace NgSharp.Tests.CustomElements;

public class LowerCasePipe : IPipe
{
    public string PipeName => "lower";

    public string Transform(string tagName, NgElement value, string argument)
    {
        return value.GetString()?.ToLower();
    }
}
