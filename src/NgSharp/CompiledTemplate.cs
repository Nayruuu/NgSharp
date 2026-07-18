using System.Text.Json;
using System.Collections.Generic;

using NgSharp.Template;

namespace NgSharp
{
    // A template parsed once (its AST cached) and rendered many times. Created via HtmlBuilder.Compile.
    // The AST is immutable and the renderer is stateless, so the same CompiledTemplate can be rendered
    // concurrently from multiple threads. Renders use the pipes/components/directives of the builder it
    // was compiled from; a component/directive registered after Compile won't be recognized (the parse
    // is a snapshot).
    public sealed class CompiledTemplate
    {
        private readonly IReadOnlyList<TemplateNode> nodes;

        private readonly HtmlBuilder builder;

        internal CompiledTemplate(IReadOnlyList<TemplateNode> nodes, HtmlBuilder builder)
        {
            this.nodes = nodes;
            this.builder = builder;
        }

        // Same model handling as HtmlBuilder.BuildFromTemplateAsync(string, object): full System.Text.Json
        // fidelity. For the fastest path, use Render(NgElement.FromObject(model)).
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
        public string Render(object model)
            => this.builder.RenderNodes(this.nodes, NgElement.FromJson(JsonSerializer.SerializeToElement(model)));

        public string Render(JsonElement model)
            => this.builder.RenderNodes(this.nodes, NgElement.FromJson(model));

        public string Render(NgElement context)
            => this.builder.RenderNodes(this.nodes, context);
    }
}
