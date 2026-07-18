using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Parsing;
using NgSharp.Directives;
using NgSharp.Components;
using NgSharp.Rendering;

namespace NgSharp
{
    /// <summary>
    /// The entry point of the engine: registers pipes/directives/components and renders Angular-style
    /// HTML templates against a data model. Get a builder pre-loaded with the built-in pipes from
    /// <see cref="Default"/>, then render with <see cref="BuildFromTemplateAsync(string, object)"/> or,
    /// for parse-once / render-many, <see cref="Compile"/>.
    /// </summary>
    public class HtmlBuilder
    {
        private readonly Dictionary<string, IPipe> pipes;

        private readonly Dictionary<string, IDirective> directives;

        private readonly Dictionary<string, IComponent> components;

        /// <summary>
        /// The pipes registered on this builder, keyed by <see cref="IPipe.PipeName"/>.
        /// </summary>
        public IReadOnlyDictionary<string, IPipe> Pipes { get => pipes; }

        /// <summary>
        /// The custom directives registered on this builder, keyed by <see cref="IDirective.DirectiveName"/>.
        /// </summary>
        public IReadOnlyDictionary<string, IDirective> Directives { get => directives; }

        /// <summary>
        /// The components registered on this builder, keyed by <see cref="IComponent.ComponentName"/>.
        /// </summary>
        public IReadOnlyDictionary<string, IComponent> Components { get => components; }

        private HtmlBuilder()
        {
            pipes = new Dictionary<string, IPipe>();
            components = new Dictionary<string, IComponent>();
            directives = new Dictionary<string, IDirective>();

            RegisterPipe<DatePipe>();
            RegisterPipe<ImagePipe>();
            RegisterPipe<UpperPipe>();
            RegisterPipe<NumberPipe>();
            RegisterPipe<LargeNumberPipe>();
        }

        /// <summary>
        /// A new builder pre-loaded with the built-in pipes (<c>date</c>, <c>image</c>, <c>upper</c>,
        /// <c>number</c>, <c>largeNumber</c>). Register your own pipes/directives/components on it before rendering.
        /// </summary>
        public static HtmlBuilder Default => new();

        /// <summary>
        /// Registers a pipe, making it usable in templates as <c>{{ value | pipeName }}</c>.
        /// </summary>
        /// <typeparam name="T">The pipe type; instantiated once via its parameterless constructor.</typeparam>
        public void RegisterPipe<T>() where T : class, IPipe, new()
        {
            var pipe = new T();

            this.pipes[pipe.PipeName] = pipe;
        }

        /// <summary>
        /// Registers a custom directive, making it usable in templates as <c>[directiveName]="expr"</c>.
        /// </summary>
        /// <typeparam name="T">The directive type; instantiated once via its parameterless constructor.</typeparam>
        public void RegisterDirective<T>() where T : class, IDirective, new()
        {
            var directive = new T();

            this.directives[directive.DirectiveName] = directive;
        }

        /// <summary>
        /// Registers a component, making it usable in templates as <c>&lt;component-name&gt;</c>.
        /// </summary>
        /// <typeparam name="T">The component type; a fresh instance is created per render.</typeparam>
        public void RegisterComponent<T>() where T : class, IComponent, new()
        {
            var component = new T();

            this.components[component.ComponentName] = component;
        }

        // Directives handled natively by the v2 parser (not via the custom-directive bridge).
        private static readonly HashSet<string> BuiltInDirectives = new HashSet<string>
        {
            "if", "for", "not-empty", "html", "attr", "style"
        };

        /// <summary>
        /// Renders <paramref name="template"/> against <paramref name="model"/>, read directly via
        /// reflection (<see cref="NgElement.FromObject(object, NgElement, string)"/>), honoring
        /// <c>[JsonPropertyName]</c> / <c>[JsonIgnore]</c> and System.Text.Json's default type mapping.
        /// </summary>
        /// <remarks>
        /// Custom <c>[JsonConverter]</c> and <c>[JsonNumberHandling]</c> are NOT applied on this path. For
        /// full System.Text.Json fidelity, serialize the model to a <see cref="JsonElement"/> yourself and
        /// use <see cref="BuildFromTemplateAsync(string, JsonElement)"/> — also the reflection-free path
        /// for Native AOT / trimming.
        /// </remarks>
        /// <param name="template">The Angular-style HTML template.</param>
        /// <param name="model">The data model bound to the template.</param>
        /// <returns>The rendered HTML.</returns>
        /// <exception cref="Exception">Thrown when <paramref name="template"/> is null or empty.</exception>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
        public Task<string> BuildFromTemplateAsync(string template, object model)
        {
            // Guard before touching the model so the empty-template error always wins.
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return RenderAsync(template, NgElement.FromObject(model));
        }

        /// <summary>
        /// Renders <paramref name="template"/> against a <see cref="JsonElement"/> model — the
        /// reflection-free ingestion path, suitable for Native AOT / trimming. (A template that renders a
        /// <c>&lt;component&gt;</c> still binds that component's properties via reflection, so preserve
        /// those members under trimming / Native AOT.)
        /// </summary>
        /// <param name="template">The Angular-style HTML template.</param>
        /// <param name="model">The data model as a parsed <see cref="JsonElement"/>.</param>
        /// <returns>The rendered HTML.</returns>
        /// <exception cref="Exception">Thrown when <paramref name="template"/> is null or empty.</exception>
        public Task<string> BuildFromTemplateAsync(string template, JsonElement model)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return RenderAsync(template, NgElement.FromJson(model));
        }

        /// <summary>
        /// Renders <paramref name="template"/> against a pre-built <see cref="NgElement"/> context — the
        /// hot-path opt-in (e.g. <c>NgElement.FromObject(model)</c>, which skips the JSON round-trip).
        /// </summary>
        /// <remarks>
        /// <see cref="NgElement.FromObject(object, NgElement, string)"/> does not populate
        /// <see cref="NgElement.Value"/> for object/array nodes, so a pipe that re-deserializes a whole
        /// object from <c>value.Value</c> won't work with a FromObject-built context.
        /// </remarks>
        /// <param name="template">The Angular-style HTML template.</param>
        /// <param name="context">The pre-built data context.</param>
        /// <returns>The rendered HTML.</returns>
        /// <exception cref="Exception">Thrown when <paramref name="template"/> is null or empty.</exception>
        public Task<string> BuildFromTemplateAsync(string template, NgElement context)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return RenderAsync(template, context);
        }

        /// <summary>
        /// Parses the template's AST once and returns a <see cref="CompiledTemplate"/> that reuses it
        /// across renders (parse-once / render-many). Rendering the same template repeatedly with
        /// different models is much cheaper this way, since parsing is the bulk of a one-shot render.
        /// </summary>
        /// <remarks>
        /// The AST is immutable and the renderer is stateless, so a <see cref="CompiledTemplate"/> is
        /// safe to render concurrently. The parse is a snapshot: a component/directive registered after
        /// <see cref="Compile"/> won't be recognized by the returned template.
        /// </remarks>
        /// <param name="template">The Angular-style HTML template to compile.</param>
        /// <returns>The compiled, reusable template.</returns>
        /// <exception cref="Exception">Thrown when <paramref name="template"/> is null or empty.</exception>
        public CompiledTemplate Compile(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            // Fold the static skeleton once here (render-many amortizes the pass); a one-shot render
            // via BuildFromTemplateAsync deliberately skips it.
            return new CompiledTemplate(TemplateProgram.Compile(Parse(template)), this);
        }

        private Task<string> RenderAsync(string template, NgElement ngElement)
        {
            return Task.FromResult(RenderNodes(Parse(template), ngElement));
        }

        private IReadOnlyList<TemplateNode> Parse(string template)
        {
            var customDirectives = directives.Keys.Where(name => !BuiltInDirectives.Contains(name));
            return TemplateParser.ParseDocument(template, components.Keys, customDirectives);
        }

        internal string RenderNodes(IReadOnlyList<TemplateNode> nodes, NgElement context)
        {
            return TemplateRenderer.Render(nodes, context, pipes, components, directives);
        }

        /// <summary>
        /// Resolves a path against <paramref name="content"/>, or parses <paramref name="instanceToken"/>
        /// as a literal when no such path exists.
        /// </summary>
        /// <param name="content">The context to resolve the path against.</param>
        /// <param name="instanceToken">A path (e.g. <c>"User.Name"</c>) or a literal value.</param>
        /// <returns>
        /// The resolved element; a number/null literal when the path is absent; or null when
        /// <paramref name="instanceToken"/> is null or whitespace.
        /// </returns>
        public NgElement Token(NgElement content, string instanceToken)
        {
            if (!string.IsNullOrWhiteSpace(instanceToken))
            {
                var element = content.SelectToken(instanceToken);

                if (element != null)
                {
                    return element;
                }
                else
                {
                    if (int.TryParse(instanceToken, out var value))
                    {
                        return NgElement.Parse(instanceToken);
                    }

                    return NgElement.Parse("null");
                }
            }

            return null;
        }

        /// <summary>
        /// Collapses insignificant whitespace between tags: strips newlines/tabs, whitespace between
        /// tags, and runs of spaces. An opt-in utility — rendering emits output verbatim and no longer
        /// applies this automatically.
        /// </summary>
        /// <param name="html">The HTML to minify.</param>
        /// <returns>The minified HTML.</returns>
        public static string MinifyHtml(string html)
        {
            var result = Regex.Replace(html, @"\r|\n|\t", "");          // supprime retours et tabulations
            result = Regex.Replace(result, @">\s+<", "><");             // supprime espaces entre balises
            result = Regex.Replace(result, @"(?<=>)\s+(?=<)", "");      // supprime indentation texte vide
            result = Regex.Replace(result, @"\s{2,}", " ");             // compresse multiples espaces en 1
            return result.Trim();
        }
    }
}
