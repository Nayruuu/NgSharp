namespace NgSharp.Pipes
{
    /// <summary>
    /// A value transform usable in templates as <c>{{ value | pipeName }}</c>, optionally with an
    /// argument (<c>{{ price | number:'C' }}</c>). Register it on a builder with
    /// <see cref="HtmlBuilder.RegisterPipe{T}"/>.
    /// </summary>
    public interface IPipe
    {
        /// <summary>
        /// The name the pipe is invoked by in a template (the token after <c>|</c>).
        /// </summary>
        string PipeName { get; }

        /// <summary>
        /// Transforms <paramref name="value"/> into the text written to the output.
        /// </summary>
        /// <param name="tagName">The host element's tag (e.g. <c>"img"</c>), or null when the pipe runs in an interpolation with no host element.</param>
        /// <param name="value">The evaluated value the pipe is applied to.</param>
        /// <param name="argument">The pipe argument (the token after <c>:</c>), or null when none was supplied.</param>
        /// <returns>The transformed text.</returns>
        string Transform(string tagName, NgElement value, string argument);
    }
}
