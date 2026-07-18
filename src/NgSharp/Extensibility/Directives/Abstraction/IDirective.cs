namespace NgSharp.Directives
{
    /// <summary>
    /// A custom attribute directive, applied in templates as <c>[directiveName]="expr"</c> to mutate
    /// the host element. Register it on a builder with <see cref="HtmlBuilder.RegisterDirective{T}"/>.
    /// </summary>
    public interface IDirective
    {
        /// <summary>
        /// The name the directive is invoked by (the token inside the <c>[ ]</c>).
        /// </summary>
        string DirectiveName { get; }

        /// <summary>
        /// Mutates the host element (typically its attributes) from the evaluated directive value —
        /// e.g. <c>[hidden]="expr"</c> adds the <c>hidden</c> attribute when <paramref name="content"/> is truthy.
        /// </summary>
        /// <param name="element">The host element to mutate.</param>
        /// <param name="content">The evaluated value of the directive expression.</param>
        void Apply(DirectiveElement element, NgElement content);
    }
}
