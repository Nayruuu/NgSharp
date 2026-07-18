using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NgSharp.Pipes;
using NgSharp.Template;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp
{
    public class HtmlBuilder
    {
        private readonly Dictionary<string, IPipe> pipes;

        private readonly Dictionary<string, IDirective> directives;

        private readonly Dictionary<string, IComponent> components;

        public IReadOnlyDictionary<string, IPipe> Pipes { get => pipes; }
        
        public IReadOnlyDictionary<string, IDirective> Directives { get => directives; }
        
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

        public static HtmlBuilder Default => new();

        public void RegisterPipe<T>() where T : class, IPipe, new()
        {
            var pipe = new T();

            this.pipes[pipe.PipeName] = pipe;
        }

        public void RegisterDirective<T>() where T : class, IDirective, new()
        {
            var directive = new T();

            this.directives[directive.DirectiveName] = directive;
        }

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

        // Serializes the model through System.Text.Json (honoring [JsonPropertyName]/[JsonIgnore]/
        // converters/etc.) straight into a JsonElement — no intermediate JSON string. For the fastest
        // path on simple POCO models, build the context yourself with NgElement.FromObject(model) and
        // pass it to the NgElement overload. Under Native AOT / trimming / file-based scripts, prefer
        // the JsonElement overload with a JsonDocument.Parse'd model (reflection-free).
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

            return RenderAsync(template, NgElement.FromJson(JsonSerializer.SerializeToElement(model)));
        }

        public Task<string> BuildFromTemplateAsync(string template, JsonElement model)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return RenderAsync(template, NgElement.FromJson(model));
        }

        // Advanced / hot-path opt-in: pass a pre-built NgElement (e.g. NgElement.FromObject(model),
        // which skips the JSON round-trip). Note FromObject does not populate Value for object/array
        // nodes, so pipes that re-deserialize a whole object from value.Value won't work with it.
        public Task<string> BuildFromTemplateAsync(string template, NgElement context)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return RenderAsync(template, context);
        }

        // Parses the template AST once and returns a CompiledTemplate that reuses it across renders
        // (parse-once / render-many). Rendering the same template repeatedly with different models is
        // much cheaper this way — parsing is the bulk of a one-shot render. The AST is immutable and
        // the renderer is stateless, so a CompiledTemplate is safe to render concurrently.
        public CompiledTemplate Compile(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return new CompiledTemplate(Parse(template), this);
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
            return MinifyHtml(TemplateRenderer.Render(nodes, context, pipes, components, directives));
        }

        #region Value Getter
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
        #endregion
        
        #region Minify
        public static string MinifyHtml(string html)
        {
            var result = Regex.Replace(html, @"\r|\n|\t", "");          // supprime retours et tabulations
            result = Regex.Replace(result, @">\s+<", "><");             // supprime espaces entre balises
            result = Regex.Replace(result, @"(?<=>)\s+(?=<)", "");      // supprime indentation texte vide
            result = Regex.Replace(result, @"\s{2,}", " ");             // compresse multiples espaces en 1
            return result.Trim();
        }
        #endregion
    }
}
