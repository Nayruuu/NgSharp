namespace NgSharp.Pipes
{
    public interface IPipe
    {
        string PipeName { get; }

        // tagName is the host element's tag (e.g. "img"), or null when the pipe runs in an
        // interpolation with no host element.
        string Transform(string tagName, NgElement value, string argument);
    }
}
