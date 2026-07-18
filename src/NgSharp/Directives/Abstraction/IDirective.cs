namespace NgSharp.Directives
{
    public interface IDirective
    {
        string DirectiveName { get; }

        // Mutates the host element (typically its attributes) from the evaluated directive value —
        // e.g. [hidden]="expr" adds the hidden attribute when expr is truthy.
        void Apply(DirectiveElement element, NgElement content);
    }
}
