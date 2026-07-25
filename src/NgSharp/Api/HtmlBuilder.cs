using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Parsing;
using NgSharp.Rendering;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp;

/// <summary>
/// The entry point of the engine: registers pipes/directives/components and renders Angular-style
/// HTML templates against a data model. Get a builder pre-loaded with the built-in pipes from
/// <see cref="Create()"/>, then render with <see cref="BuildFromTemplate(string, object, TemplateOptions)"/>
/// or, for parse-once / render-many, <see cref="Compile(string, TemplateOptions)"/>. Everything
/// tunable — dialect, strict mode, culture, resource caps — travels in one optional
/// <see cref="TemplateOptions"/> argument. Rendering is entirely synchronous — it is CPU-bound,
/// with nothing to await. Non-HTML output
/// (plain-text emails, JSON, CSV…) renders through the same methods with
/// <see cref="TemplateMode.Text"/> in the options.
/// </summary>
public class HtmlBuilder
{
    #region Fields

    // Handled natively by the parser — never routed through the custom-directive bridge.
    private static readonly HashSet<string> BuiltInDirectives = new HashSet<string>
    {
        "if", "for", "not-empty", "empty", "html", "attr", "style"
    };

    // Not readonly: RegisterPipe swaps in a fresh dictionary (copy-on-write) — see its comment.
    private Dictionary<string, IPipe> _pipes;

    private readonly Dictionary<string, IDirective> _directives;

    private readonly Dictionary<string, IComponent> _components;

    // Mirror of _components carrying the trimmer-annotated Type — the render-time activation source.
    private readonly Dictionary<string, ComponentRegistration> _componentRegistrations;

    // The builder-level strict default (Create(strict: true)): applied by every Build/Compile whose
    // options leave Strict null — an explicit true/false in the options always wins.
    private readonly bool _strict;

    #endregion

    #region Properties

    /// <summary>
    /// The pipes registered on this builder, keyed by <see cref="IPipe.PipeName"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IPipe> Pipes { get => _pipes; }

    /// <summary>
    /// The custom directives registered on this builder, keyed by <see cref="IDirective.DirectiveName"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IDirective> Directives { get => _directives; }

    /// <summary>
    /// The components registered on this builder, keyed by <see cref="IComponent.ComponentName"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IComponent> Components { get => _components; }

    #endregion

    #region Constructors

    private HtmlBuilder(bool strict = false)
    {
        _strict = strict;
        _pipes = new Dictionary<string, IPipe>();
        _components = new Dictionary<string, IComponent>();
        _componentRegistrations = new Dictionary<string, ComponentRegistration>();
        _directives = new Dictionary<string, IDirective>();

        RegisterPipe<DatePipe>();
        RegisterPipe<ImagePipe>();
        RegisterPipe<UpperPipe>();
        RegisterPipe<NumberPipe>();
        RegisterPipe<LargeNumberPipe>();
        RegisterPipe<JsonPipe>();
        RegisterPipe<DefaultPipe>();
        RegisterPipe<CurrencyPipe>();
        RegisterPipe<LowerPipe>();
        RegisterPipe<TruncatePipe>();
        RegisterPipe<JoinPipe>();
        RegisterPipe<TitleCasePipe>();
        RegisterPipe<PadPipe>();
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Creates a builder pre-loaded with the built-in pipes (<c>date</c>, <c>image</c>, <c>upper</c>,
    /// <c>number</c>, <c>largeNumber</c>, <c>json</c>, <c>default</c>, <c>currency</c>, <c>lower</c>,
    /// <c>truncate</c>, <c>join</c>, <c>titlecase</c>, <c>pad</c>). Register your own
    /// pipes/directives/components on the returned instance before rendering.
    /// </summary>
    /// <returns>A new, independent builder.</returns>
    public static HtmlBuilder Create() => new();

    /// <summary>
    /// Same as <see cref="Create()"/>, with the builder's strict default set once: a strict builder
    /// applies strict rendering/compiling to EVERY <c>BuildFromTemplate</c> /
    /// <see cref="Compile(string, TemplateOptions)"/> call without repeating the flag. An explicit
    /// <see cref="TemplateOptions.Strict"/> in a call's options still overrides it (call wins over
    /// builder) — pass <c>new TemplateOptions { Strict = false }</c> to render leniently from a
    /// strict builder.
    /// </summary>
    /// <param name="strict">The builder-wide strict default (see <see cref="TemplateOptions.Strict"/>).</param>
    /// <returns>A new, independent builder.</returns>
    public static HtmlBuilder Create(bool strict) => new(strict);

    /// <summary>
    /// Registers a pipe, making it usable in templates as <c>{{ value | pipeName }}</c>.
    /// </summary>
    /// <typeparam name="T">The pipe type; instantiated once via its parameterless constructor.</typeparam>
    public void RegisterPipe<T>() where T : class, IPipe, new()
    {
        RegisterPipe(new T());
    }

    /// <summary>
    /// Registers an already-built pipe instance — the DI-friendly twin of <see cref="RegisterPipe{T}"/>
    /// for pipes carrying constructor-injected configuration or services.
    /// </summary>
    /// <remarks>
    /// The instance is SHARED by every render of this builder (and of every <see cref="CompiledTemplate"/>
    /// compiled from it), potentially concurrently — it must be thread-safe, ideally immutable/stateless.
    /// </remarks>
    /// <param name="pipe">The pipe instance, invoked in templates under its <see cref="IPipe.PipeName"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pipe"/> is null.</exception>
    public void RegisterPipe(IPipe pipe)
    {
        if (pipe is null)
        {
            throw new ArgumentNullException(nameof(pipe));
        }

        // Copy-on-write: the per-AST resolved-pipe memos (PipeExpression) are keyed on the registry
        // INSTANCE, so replacing a pipe under an already-resolved name must swap the dictionary —
        // an in-place write would leave those hit memos serving the old implementation (and mutate
        // a dictionary that a concurrent render may be reading). The atomic reference store gives
        // in-flight renders a coherent snapshot; the next render picks up the new registry.
        _pipes = new Dictionary<string, IPipe>(_pipes) { [pipe.PipeName] = pipe };
    }

    /// <summary>
    /// Registers a custom directive, making it usable in templates as <c>[directiveName]="expr"</c>.
    /// </summary>
    /// <typeparam name="T">The directive type; instantiated once via its parameterless constructor.</typeparam>
    public void RegisterDirective<T>() where T : class, IDirective, new()
    {
        RegisterDirective(new T());
    }

    /// <summary>
    /// Registers an already-built directive instance — the DI-friendly twin of
    /// <see cref="RegisterDirective{T}"/> for directives carrying constructor-injected configuration.
    /// </summary>
    /// <remarks>
    /// The instance is SHARED by every render of this builder (and of every <see cref="CompiledTemplate"/>
    /// compiled from it), potentially concurrently — it must be thread-safe, ideally immutable/stateless.
    /// </remarks>
    /// <param name="directive">The directive instance, invoked in templates under its <see cref="IDirective.DirectiveName"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directive"/> is null.</exception>
    public void RegisterDirective(IDirective directive)
    {
        if (directive is null)
        {
            throw new ArgumentNullException(nameof(directive));
        }

        _directives[directive.DirectiveName] = directive;
    }

    /// <summary>
    /// Registers a component, making it usable in templates as <c>&lt;component-name&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The component's <see cref="IComponent.Render"/> output is trusted raw HTML — the engine injects
    /// it verbatim, without escaping (same contract as the <c>[html]</c> binding). Escape any
    /// user-supplied data inside your component (e.g. <c>System.Net.WebUtility.HtmlEncode</c>) before
    /// embedding it in the returned markup.
    /// </remarks>
    /// <typeparam name="T">The component type; a fresh instance is created per render.</typeparam>
    public void RegisterComponent<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] T>() where T : class, IComponent, new()
    {
        RegisterComponent(new T());
    }

    /// <summary>
    /// Registers an already-built component instance — the DI-friendly twin of
    /// <see cref="RegisterComponent{T}()"/>. Call it with the CONCRETE component type: the inferred
    /// <typeparamref name="T"/> is what carries the trimmer annotations and gets activated per render.
    /// </summary>
    /// <remarks>
    /// The registered instance is a PROTOTYPE, not the render target: exactly like
    /// <see cref="RegisterComponent{T}()"/>, every render activates a FRESH <typeparamref name="T"/>
    /// through its public parameterless constructor (which <typeparamref name="T"/> must therefore
    /// still have) and binds the element's <c>[prop]</c> attributes on that fresh instance — state
    /// carried by the registered instance (e.g. constructor arguments) does not flow into renders.
    /// The component's <see cref="IComponent.Render"/> output stays trusted raw HTML, injected
    /// verbatim — see <see cref="RegisterComponent{T}()"/>.
    /// </remarks>
    /// <typeparam name="T">The concrete component type; a fresh instance is created per render.</typeparam>
    /// <param name="component">The component instance whose <see cref="IComponent.ComponentName"/> keys the registration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="component"/>'s runtime type is not exactly <typeparamref name="T"/> — register with the concrete type.</exception>
    public void RegisterComponent<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] T>(T component) where T : class, IComponent
    {
        if (component is null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        if (component.GetType() != typeof(T))
        {
            throw new ArgumentException(
                $"RegisterComponent<{typeof(T).Name}> received a {component.GetType().Name} instance — register with the concrete type: the inferred type parameter drives per-render activation.",
                nameof(component));
        }

        _components[component.ComponentName] = component;
        _componentRegistrations[component.ComponentName] = new ComponentRegistration(component, typeof(T));
    }

    /// <summary>
    /// Renders <paramref name="template"/> against <paramref name="model"/>, read directly via
    /// reflection (<see cref="NgElement.FromObject(object)"/>), honoring
    /// <c>[JsonPropertyName]</c> / <c>[JsonIgnore]</c> and System.Text.Json's default type mapping.
    /// Everything tunable — dialect, strict mode, culture, resource caps — travels in
    /// <paramref name="options"/> (see <see cref="TemplateOptions"/>); omitting it renders lenient
    /// HTML under the ambient culture with no caps. Rendering is CPU-bound and completes
    /// synchronously.
    /// </summary>
    /// <remarks>
    /// Custom <c>[JsonConverter]</c> and <c>[JsonNumberHandling]</c> are NOT applied on this path. For
    /// full System.Text.Json fidelity, serialize the model to a <see cref="JsonElement"/> yourself and
    /// use <see cref="BuildFromTemplate(string, JsonElement, TemplateOptions)"/> — also the
    /// reflection-free path for Native AOT / trimming.
    /// </remarks>
    /// <param name="template">The template, written in the dialect of <see cref="TemplateOptions.Mode"/>.</param>
    /// <param name="model">The data model bound to the template.</param>
    /// <param name="options">The render options; null (the default) means <see cref="TemplateOptions.Default"/>.</param>
    /// <returns>The rendered output.</returns>
    /// <exception cref="NgSharpException">Thrown when <paramref name="template"/> is null or empty, when a strict
    /// render fails validation or hits a missing path, or when a <see cref="TemplateOptions.Limits"/> cap is exceeded.</exception>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
    public string BuildFromTemplate(string template, object model, TemplateOptions options = null)
    {
        // Guard before touching the model so the empty-template error always wins.
        GuardTemplate(template);

        options ??= TemplateOptions.Default;

        using var swap = new CultureSwap(options.Culture);

        return Render(template, NgElement.FromObject(model), options.Mode ?? TemplateMode.Html, options.Limits, options.Strict);
    }

    /// <summary>
    /// Renders <paramref name="template"/> against a <see cref="JsonElement"/> model — the
    /// reflection-free ingestion path, suitable for Native AOT / trimming. (A template that renders a
    /// <c>&lt;component&gt;</c> still binds that component's properties via reflection, so preserve
    /// those members under trimming / Native AOT.) Everything tunable travels in
    /// <paramref name="options"/> (see <see cref="TemplateOptions"/>).
    /// </summary>
    /// <param name="template">The template, written in the dialect of <see cref="TemplateOptions.Mode"/>.</param>
    /// <param name="model">The data model as a parsed <see cref="JsonElement"/>.</param>
    /// <param name="options">The render options; null (the default) means <see cref="TemplateOptions.Default"/>.</param>
    /// <returns>The rendered output.</returns>
    /// <exception cref="NgSharpException">Thrown when <paramref name="template"/> is null or empty, when a strict
    /// render fails validation or hits a missing path, or when a <see cref="TemplateOptions.Limits"/> cap is exceeded.</exception>
    public string BuildFromTemplate(string template, JsonElement model, TemplateOptions options = null)
    {
        GuardTemplate(template);

        options ??= TemplateOptions.Default;

        using var swap = new CultureSwap(options.Culture);

        return Render(template, NgElement.FromJson(model), options.Mode ?? TemplateMode.Html, options.Limits, options.Strict);
    }

    /// <summary>
    /// Renders <paramref name="template"/> against a pre-built <see cref="NgElement"/> context — the
    /// hot-path opt-in (e.g. <c>NgElement.FromObject(model)</c>, which skips the JSON round-trip).
    /// Everything tunable travels in <paramref name="options"/> (see <see cref="TemplateOptions"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="NgElement.FromObject(object)"/> does not populate
    /// <see cref="NgElement.Value"/> for object/array nodes, so a pipe that re-deserializes a whole
    /// object from <c>value.Value</c> won't work with a FromObject-built context.
    /// </remarks>
    /// <param name="template">The template, written in the dialect of <see cref="TemplateOptions.Mode"/>.</param>
    /// <param name="context">The pre-built data context.</param>
    /// <param name="options">The render options; null (the default) means <see cref="TemplateOptions.Default"/>.</param>
    /// <returns>The rendered output.</returns>
    /// <exception cref="NgSharpException">Thrown when <paramref name="template"/> is null or empty, when a strict
    /// render fails validation or hits a missing path, or when a <see cref="TemplateOptions.Limits"/> cap is exceeded.</exception>
    public string BuildFromTemplate(string template, NgElement context, TemplateOptions options = null)
    {
        GuardTemplate(template);

        options ??= TemplateOptions.Default;

        using var swap = new CultureSwap(options.Culture);

        return Render(template, context, options.Mode ?? TemplateMode.Html, options.Limits, options.Strict);
    }

    /// <summary>
    /// Parses the template's AST once and returns a <see cref="CompiledTemplate"/> that reuses it
    /// across renders (parse-once / render-many). Rendering the same template repeatedly with
    /// different models is much cheaper this way, since parsing is the bulk of a one-shot render.
    /// </summary>
    /// <remarks>
    /// How the compile reads <paramref name="options"/>: <see cref="TemplateOptions.Mode"/> and
    /// <see cref="TemplateOptions.Strict"/> act NOW (dialect of the parse; validation gate + the
    /// returned template's <see cref="CompiledTemplate.Strict"/>). <see cref="TemplateOptions.Culture"/>
    /// and <see cref="TemplateOptions.Limits"/> are MEMORIZED as the compiled template's render
    /// defaults — every render uses them unless its own options override them
    /// (see <see cref="CompiledTemplate.Render(object, TemplateOptions)"/>).
    /// The AST is immutable and the renderer is stateless, so a <see cref="CompiledTemplate"/> is
    /// safe to render concurrently. The parse is a snapshot: a component/directive registered after
    /// <see cref="Compile(string, TemplateOptions)"/> won't be recognized by the returned template.
    /// </remarks>
    /// <param name="template">The template to compile, written in the dialect of <see cref="TemplateOptions.Mode"/>.</param>
    /// <param name="options">The compile options; null (the default) means <see cref="TemplateOptions.Default"/>.</param>
    /// <returns>The compiled, reusable template.</returns>
    /// <exception cref="NgSharpException">Thrown when <paramref name="template"/> is null or empty, or —
    /// with strict mode — when the template has validation errors.</exception>
    public CompiledTemplate Compile(string template, TemplateOptions options = null)
    {
        GuardTemplate(template);

        options ??= TemplateOptions.Default;

        var mode = options.Mode ?? TemplateMode.Html;

        // Call wins over builder: null inherits the builder-level default (Create(strict: true)).
        var effectiveStrict = options.Strict ?? _strict;

        if (effectiveStrict)
        {
            ThrowOnValidationErrors(template, mode);
        }

        var nodes = Parse(template, mode);

        return new CompiledTemplate(nodes, mode.CollectTemplates(template, nodes), this, mode, effectiveStrict, options.Culture, options.Limits);
    }

    /// <summary>
    /// Parses <paramref name="template"/> and reports every problem the lenient renderer would swallow
    /// silently, without throwing: unclosed <c>{{</c> interpolations, empty or unparsable expressions,
    /// <c>@for (x in …)</c> (NgSharp loops use <c>of</c>), unclosed <c>@if</c>/<c>@for</c> blocks,
    /// orphan <c>@else</c>/<c>[else-if]</c>/<c>[else]</c> branches, malformed pipe segments, and — as
    /// warnings — pipes not registered on this builder and dashed tags (<c>&lt;x-y&gt;</c>) not
    /// registered as components (legitimate custom elements can ignore that one). An empty list means
    /// the template is clean.
    /// </summary>
    /// <remarks>
    /// Made for CI and template editors: validate tenant-authored or designer-authored templates
    /// before they ship, with <see cref="TemplateDiagnostic.Position"/> pointing into the source.
    /// Validation parses against THIS builder's registrations, so register your pipes, components and
    /// directives first. A strict compile (<see cref="Compile(string, TemplateOptions)"/>) runs
    /// this validation and throws on any <see cref="DiagnosticSeverity.Error"/>.
    /// </remarks>
    /// <param name="template">The template, written in the <paramref name="mode"/> dialect.</param>
    /// <param name="mode">The template dialect; null defaults to <see cref="TemplateMode.Html"/>.</param>
    /// <returns>The findings, ordered by position; empty when the template is clean.</returns>
    public IReadOnlyList<TemplateDiagnostic> Validate(string template, TemplateMode mode = null)
    {
        if (string.IsNullOrEmpty(template))
        {
            return new[] { new TemplateDiagnostic(DiagnosticSeverity.Error, "Can't replace an empty html template", 0) };
        }

        mode ??= TemplateMode.Html;

        // The collector is ambient (thread-static) for the duration of THIS parse only — the parsers
        // read it in cold branches and never mutate their output; the parsed program is discarded.
        var collector = new DiagnosticCollector(template, _pipes.Keys);
        DiagnosticCollector.SetCurrent(collector);
        try
        {
            Parse(template, mode);
        }
        catch (Exception exception)
        {
            // Validate never throws: an unexpected parser failure IS a diagnostic.
            collector.Report(DiagnosticSeverity.Error, $"The template failed to parse: {exception.Message}", 0);
        }
        finally
        {
            DiagnosticCollector.ClearCurrent();
        }

        // Only the HTML dialect reads markup, so only it can mistake an unregistered component for markup.
        if (ReferenceEquals(mode, TemplateMode.Html))
        {
            CollectUnregisteredDashedTags(template, collector);
        }

        return collector.Finish();
    }

    #endregion

    #region Private methods

    private static void GuardTemplate(string template)
    {
        // The historical message, kept verbatim — callers match on it.
        if (string.IsNullOrEmpty(template))
        {
            throw new NgSharpException("Can't replace an empty html template");
        }
    }

    private string Render(string template, NgElement ngElement, TemplateMode mode, RenderLimits limits, bool? strict)
    {
        // Call wins over builder: null inherits the builder-level default (Create(strict: true)).
        var effectiveStrict = strict ?? _strict;

        // Strict is a one-shot render's compile gate too: refuse a template Validate flags as broken
        // BEFORE rendering it (same contract as Compile with Strict = true).
        if (effectiveStrict)
        {
            ThrowOnValidationErrors(template, mode);
        }

        var nodes = Parse(template, mode);

        // <ng-template> handling is the mode's call (HTML-only, with a safe source fast-out inside).
        var templates = mode.CollectTemplates(template, nodes);

        // No adaptive hint on a cold render: size the initial buffer from the template (output is rarely smaller).
        var initialCapacity = Math.Max(1024, template.Length);

        return RenderNodes(nodes, ngElement, templates, initialCapacity, limits, effectiveStrict);
    }

    // Validation-only source scan: a dashed open tag that is NOT a registered component is legal
    // markup (real custom elements pass through verbatim), but when the author MEANT a component the
    // render silently emits the raw tag — surface it as a Warning (never an Error), once per tag name.
    private void CollectUnregisteredDashedTags(string template, DiagnosticCollector collector)
    {
        HashSet<string> reported = null;

        for (var i = 0; i < template.Length - 1; i++)
        {
            if (template[i] != '<')
            {
                continue;
            }

            // <!-- comment -->: nothing inside is a tag — jump past it (or to the end when unclosed).
            if (template[i + 1] == '!')
            {
                if (i + 3 < template.Length && template[i + 2] == '-' && template[i + 3] == '-')
                {
                    var close = template.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = close < 0 ? template.Length : close + 2;
                }

                continue;
            }

            // Only an OPEN tag names a component: '</x-y>' ('/' fails the letter test) and stray '<' skip here.
            var start = i + 1;
            if (char.IsLetter(template[start]) == false)
            {
                continue;
            }

            var end = start + 1;
            while (end < template.Length && (char.IsLetterOrDigit(template[end]) || template[end] == '-'))
            {
                end++;
            }

            var name = template.Substring(start, end - start);
            i = end - 1;

            if (name.IndexOf('-') < 0)
            {
                continue;
            }

            var tag = name.ToLowerInvariant();
            if (tag == "ng-template" || tag == "ng-container" || _components.ContainsKey(tag))
            {
                continue;
            }

            if ((reported ??= new HashSet<string>()).Add(tag))
            {
                collector.Report(DiagnosticSeverity.Warning,
                    $"Unknown dashed tag '<{tag}>' — if '<{tag}>' is a component, register it before Compile (RegisterComponent). A genuine custom element renders verbatim and can ignore this warning.",
                    start - 1);
            }
        }
    }

    // The strict-compile gate: any Error-severity diagnostic refuses the template, all errors listed.
    private void ThrowOnValidationErrors(string template, TemplateMode mode)
    {
        var diagnostics = Validate(template, mode);

        var messages = new List<string>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                messages.Add($"  - {diagnostic}");
            }
        }

        if (messages.Count > 0)
        {
            throw new NgSharpException(
                $"Strict mode: the template has {messages.Count} validation error(s):\n{string.Join("\n", messages)}");
        }
    }

    // The parse output IS the folded program — never re-fold it (a second fold pass has no ConstNode
    // case and would silently drop the folded runs). The mode instance carries the dialect's parser.
    private IReadOnlyList<TemplateNode> Parse(string template, TemplateMode mode)
    {
        var customDirectives = _directives.Keys.Where(name => BuiltInDirectives.Contains(name) == false);

        return mode.Parse(template, _components.Keys, customDirectives);
    }

    #endregion

    #region Internal methods

    internal string RenderNodes(IReadOnlyList<TemplateNode> nodes, NgElement context, IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates, int initialCapacity = 256, RenderLimits limits = null, bool strict = false)
    {
        return TemplateRenderer.Render(nodes, context, _pipes, _componentRegistrations, _directives, templates, initialCapacity, limits, strict);
    }

    // Sink twins of RenderNodes for CompiledTemplate — culture travels down to the renderer, which
    // brackets the synchronous walk with it (see TemplateRenderer.RenderTo/RenderToAsync).
    internal int RenderNodesTo(TextWriter sink, IReadOnlyList<TemplateNode> nodes, NgElement context, IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates, CultureInfo culture, int initialCapacity = 256, RenderLimits limits = null, bool strict = false)
    {
        return TemplateRenderer.RenderTo(sink, nodes, context, culture, _pipes, _componentRegistrations, _directives, templates, initialCapacity, limits, strict);
    }

    internal Task<int> RenderNodesToAsync(TextWriter sink, IReadOnlyList<TemplateNode> nodes, NgElement context, IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates, CultureInfo culture, int initialCapacity = 256, RenderLimits limits = null, bool strict = false, CancellationToken cancellationToken = default)
    {
        return TemplateRenderer.RenderToAsync(sink, nodes, context, culture, _pipes, _componentRegistrations, _directives, templates, initialCapacity, limits, strict, cancellationToken);
    }

    #endregion
}
