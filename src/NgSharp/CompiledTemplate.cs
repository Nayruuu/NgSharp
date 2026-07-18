using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Ast;

namespace NgSharp
{
    /// <summary>
    /// A template parsed once (its AST cached) and rendered many times. Created via
    /// <see cref="HtmlBuilder.Compile"/>.
    /// </summary>
    /// <remarks>
    /// The AST is immutable and the renderer is stateless, so the same <see cref="CompiledTemplate"/>
    /// can be rendered concurrently from multiple threads. Renders use the pipes/components/directives
    /// of the builder it was compiled from; a component or directive registered after
    /// <see cref="HtmlBuilder.Compile"/> won't be recognized (the parse is a snapshot).
    /// </remarks>
    public sealed class CompiledTemplate
    {
        private readonly IReadOnlyList<TemplateNode> nodes;

        private readonly HtmlBuilder builder;

        internal CompiledTemplate(IReadOnlyList<TemplateNode> nodes, HtmlBuilder builder)
        {
            this.nodes = nodes;
            this.builder = builder;
        }

        /// <summary>
        /// Renders the compiled template against <paramref name="model"/>, read directly via reflection —
        /// same as <see cref="HtmlBuilder.BuildFromTemplateAsync(string, object)"/> (see its remarks for the
        /// System.Text.Json fidelity caveat).
        /// </summary>
        /// <remarks>
        /// To avoid rebuilding the context on every render, build it once with
        /// <c>NgElement.FromObject(model)</c> and call <see cref="Render(NgElement)"/>.
        /// </remarks>
        /// <param name="model">The data model bound to the template.</param>
        /// <returns>The rendered HTML.</returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
        public string Render(object model)
            => this.builder.RenderNodes(this.nodes, NgElement.FromObject(model));

        /// <summary>
        /// Renders the compiled template against a <see cref="JsonElement"/> model — the reflection-free
        /// ingestion path, suitable for Native AOT / trimming. (A template that renders a
        /// <c>&lt;component&gt;</c> still binds that component's properties via reflection, so preserve
        /// those members under trimming / Native AOT.)
        /// </summary>
        /// <param name="model">The data model as a parsed <see cref="JsonElement"/>.</param>
        /// <returns>The rendered HTML.</returns>
        public string Render(JsonElement model)
            => this.builder.RenderNodes(this.nodes, NgElement.FromJson(model));

        /// <summary>
        /// Renders the compiled template against a pre-built <see cref="NgElement"/> context — the
        /// hot-path opt-in (e.g. <c>NgElement.FromObject(model)</c>).
        /// </summary>
        /// <param name="context">The pre-built data context.</param>
        /// <returns>The rendered HTML.</returns>
        public string Render(NgElement context)
            => this.builder.RenderNodes(this.nodes, context);
    }
}
