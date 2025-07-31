using AngleSharp.Dom;

namespace NgSharp.Pipes
{
    public interface IPipe
    {
        string PipeName { get; }

        string Transform(IElement childElement, NgElement value, string argument);
    }
}
