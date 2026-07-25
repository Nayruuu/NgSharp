using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Rendering;

namespace NgSharp;

/// <summary>
/// A template parsed once (its AST cached) and rendered many times. Created via
/// <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/>.
/// </summary>
/// <remarks>
/// The AST is immutable and rendering mutates no shared state that affects the output, so the same
/// <see cref="CompiledTemplate"/> can be rendered concurrently from multiple threads (the only mutable
/// field is a benign output-capacity hint that only ever sizes a buffer). Renders use the
/// pipes/components/directives of the builder it was compiled from; a component or directive registered
/// after <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/> won't be recognized (the parse is
/// a snapshot). The <see cref="TemplateOptions.Culture"/> and <see cref="TemplateOptions.Limits"/>
/// given at compile time are this template's render defaults; per-render options override them.
/// </remarks>
public sealed class CompiledTemplate
{
    #region Fields

    private readonly IReadOnlyList<TemplateNode> _nodes;

    // Named <ng-template> fragments in this template, collected once at compile (null when none).
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> _templates;

    private readonly HtmlBuilder _builder;

    // The render defaults memorized at compile (TemplateOptions.Culture/Limits): every render uses
    // them unless its own options override them. Null = ambient culture / no caps.
    private readonly CultureInfo _culture;

    private readonly RenderLimits _limits;

    // Previous render's length, fed back as the initial capacity. A plain int is deliberate: it only
    // ever sizes a buffer, never affects output, so the race under concurrent renders is benign.
    private int _capacityHint = 256;

    #endregion

    #region Properties

    /// <summary>
    /// The <see cref="TemplateMode"/> the template was compiled with — captured at
    /// <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/> time and baked into the compiled
    /// program, which is why <see cref="Render(object, TemplateOptions)"/> ignores
    /// <see cref="TemplateOptions.Mode"/>.
    /// </summary>
    public TemplateMode Mode { get; }

    /// <summary>
    /// True when the template was compiled with <see cref="TemplateOptions.Strict"/> set (or from a
    /// strict builder, <see cref="HtmlBuilder.Create(bool)"/>): the template passed validation at
    /// compile time, and every render throws <see cref="NgSharpException"/> when a template path does
    /// not exist in the model — instead of silently rendering empty. A property that IS present with
    /// a null value still renders empty; paths guarded with <c>?.</c> are exempt. A per-render
    /// <see cref="TemplateOptions.Strict"/> overrides the render-time half of this default.
    /// </summary>
    public bool Strict { get; }

    #endregion

    #region Constructors

    internal CompiledTemplate(IReadOnlyList<TemplateNode> nodes, IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates, HtmlBuilder builder, TemplateMode mode, bool strict = false, CultureInfo culture = null, RenderLimits limits = null)
    {
        _nodes = nodes;
        _templates = templates;
        _builder = builder;
        _culture = culture;
        _limits = limits;
        Mode = mode;
        Strict = strict;
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Renders the compiled template against <paramref name="model"/>, read directly via reflection —
    /// same as <see cref="HtmlBuilder.BuildFromTemplate(string, object, TemplateOptions)"/> (see its
    /// remarks for the System.Text.Json fidelity caveat).
    /// </summary>
    /// <remarks>
    /// How the render reads <paramref name="options"/>: <see cref="TemplateOptions.Culture"/>,
    /// <see cref="TemplateOptions.Limits"/> and <see cref="TemplateOptions.Strict"/> override the
    /// defaults memorized at compile time (null falls back to them). The dialect was baked in at
    /// compile (<see cref="Mode"/>), so an explicit <see cref="TemplateOptions.Mode"/> that
    /// CONTRADICTS it throws <see cref="NgSharpException"/> — compile the template in that mode
    /// instead (null, or the compiled mode itself, is fine: share one options instance between
    /// Compile and Render freely). A per-render <c>Strict</c> only toggles render-time strictness
    /// (missing paths, bad conditions, division by zero) — the compile-time validation gate already
    /// ran, or not, at compile.
    /// To avoid rebuilding the context on every render, build it once with
    /// <c>NgElement.FromObject(model)</c> and call <see cref="Render(NgElement, TemplateOptions)"/>.
    /// </remarks>
    /// <param name="model">The data model bound to the template.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <returns>The rendered output.</returns>
    /// <exception cref="NgSharpException">Thrown when a strict render hits a missing path, when a
    /// <see cref="TemplateOptions.Limits"/> cap is exceeded, or when <paramref name="options"/> names
    /// a <see cref="TemplateOptions.Mode"/> contradicting the compiled <see cref="Mode"/>.</exception>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
    public string Render(object model, TemplateOptions options = null)
    {
        GuardMode(options);

        using var swap = new CultureSwap(options?.Culture ?? _culture);

        var html = _builder.RenderNodes(_nodes, NgElement.FromObject(model), _templates, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict);
        _capacityHint = html.Length;

        return html;
    }

    /// <summary>
    /// Renders the compiled template against a <see cref="JsonElement"/> model — the reflection-free
    /// ingestion path, suitable for Native AOT / trimming. (A template that renders a
    /// <c>&lt;component&gt;</c> still binds that component's properties via reflection, so preserve
    /// those members under trimming / Native AOT.) Reads <paramref name="options"/> exactly like
    /// <see cref="Render(object, TemplateOptions)"/> — see its remarks.
    /// </summary>
    /// <param name="model">The data model as a parsed <see cref="JsonElement"/>.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <returns>The rendered output.</returns>
    /// <exception cref="NgSharpException">Thrown when a strict render hits a missing path, when a
    /// <see cref="TemplateOptions.Limits"/> cap is exceeded, or when <paramref name="options"/> names
    /// a <see cref="TemplateOptions.Mode"/> contradicting the compiled <see cref="Mode"/>.</exception>
    public string Render(JsonElement model, TemplateOptions options = null)
    {
        GuardMode(options);

        using var swap = new CultureSwap(options?.Culture ?? _culture);

        var html = _builder.RenderNodes(_nodes, NgElement.FromJson(model), _templates, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict);
        _capacityHint = html.Length;

        return html;
    }

    /// <summary>
    /// Renders the compiled template against a pre-built <see cref="NgElement"/> context — the
    /// hot-path opt-in (e.g. <c>NgElement.FromObject(model)</c>). Reads <paramref name="options"/>
    /// exactly like <see cref="Render(object, TemplateOptions)"/> — see its remarks.
    /// </summary>
    /// <param name="context">The pre-built data context.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <returns>The rendered output.</returns>
    /// <exception cref="NgSharpException">Thrown when a strict render hits a missing path, when a
    /// <see cref="TemplateOptions.Limits"/> cap is exceeded, or when <paramref name="options"/> names
    /// a <see cref="TemplateOptions.Mode"/> contradicting the compiled <see cref="Mode"/>.</exception>
    public string Render(NgElement context, TemplateOptions options = null)
    {
        GuardMode(options);

        using var swap = new CultureSwap(options?.Culture ?? _culture);

        var html = _builder.RenderNodes(_nodes, context, _templates, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict);
        _capacityHint = html.Length;

        return html;
    }

    /// <summary>
    /// Renders the compiled template against <paramref name="model"/> (read via reflection, like
    /// <see cref="Render(object, TemplateOptions)"/>) and writes the result to
    /// <paramref name="writer"/>. Nothing reaches the writer until the render has fully succeeded:
    /// a throwing render writes zero characters (atomic). What the sink saves is the final output
    /// string.
    /// </summary>
    /// <param name="model">The data model bound to the template.</param>
    /// <param name="writer">The sink the rendered output is written to.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="NgSharpException">Same conditions as <see cref="Render(object, TemplateOptions)"/> —
    /// and the writer receives nothing when it throws.</exception>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
    public void Render(object model, TextWriter writer, TemplateOptions options = null)
    {
        GuardMode(options);

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _capacityHint = _builder.RenderNodesTo(writer, _nodes, NgElement.FromObject(model), _templates, options?.Culture ?? _culture, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict);
    }

    /// <summary>
    /// Renders the compiled template against <paramref name="model"/> and writes the result to
    /// <paramref name="writer"/>, awaiting the write: the walk is CPU-bound and synchronous; the
    /// await is the write to your writer (real I/O). Nothing reaches the writer until the render has
    /// fully succeeded: a throwing render writes zero characters (atomic). What the sink saves is
    /// the final output string.
    /// </summary>
    /// <param name="model">The data model bound to the template.</param>
    /// <param name="writer">The sink the rendered output is written to.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <param name="cancellationToken">Checked before the render starts and passed to the writer — an
    /// already-canceled token throws before anything is rendered or written.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="NgSharpException">Same conditions as <see cref="Render(object, TemplateOptions)"/> —
    /// and the writer receives nothing when it throws.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Serializes the model with reflection-based System.Text.Json. For trimming / Native AOT use the JsonElement or NgElement overload.")]
#endif
    public async Task RenderAsync(object model, TextWriter writer, TemplateOptions options = null, CancellationToken cancellationToken = default)
    {
        GuardMode(options);

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _capacityHint = await _builder.RenderNodesToAsync(writer, _nodes, NgElement.FromObject(model), _templates, options?.Culture ?? _culture, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the compiled template against a <see cref="JsonElement"/> model (the reflection-free
    /// path, like <see cref="Render(JsonElement, TemplateOptions)"/>) and writes the result to
    /// <paramref name="writer"/>. Nothing reaches the writer until the render has fully succeeded:
    /// a throwing render writes zero characters (atomic). What the sink saves is the final output
    /// string.
    /// </summary>
    /// <param name="model">The data model as a parsed <see cref="JsonElement"/>.</param>
    /// <param name="writer">The sink the rendered output is written to.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="NgSharpException">Same conditions as <see cref="Render(object, TemplateOptions)"/> —
    /// and the writer receives nothing when it throws.</exception>
    public void Render(JsonElement model, TextWriter writer, TemplateOptions options = null)
    {
        GuardMode(options);

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _capacityHint = _builder.RenderNodesTo(writer, _nodes, NgElement.FromJson(model), _templates, options?.Culture ?? _culture, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict);
    }

    /// <summary>
    /// Renders the compiled template against a <see cref="JsonElement"/> model and writes the result
    /// to <paramref name="writer"/>, awaiting the write: the walk is CPU-bound and synchronous; the
    /// await is the write to your writer (real I/O). Nothing reaches the writer until the render has
    /// fully succeeded: a throwing render writes zero characters (atomic). What the sink saves is
    /// the final output string.
    /// </summary>
    /// <param name="model">The data model as a parsed <see cref="JsonElement"/>.</param>
    /// <param name="writer">The sink the rendered output is written to.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <param name="cancellationToken">Checked before the render starts and passed to the writer — an
    /// already-canceled token throws before anything is rendered or written.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="NgSharpException">Same conditions as <see cref="Render(object, TemplateOptions)"/> —
    /// and the writer receives nothing when it throws.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task RenderAsync(JsonElement model, TextWriter writer, TemplateOptions options = null, CancellationToken cancellationToken = default)
    {
        GuardMode(options);

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _capacityHint = await _builder.RenderNodesToAsync(writer, _nodes, NgElement.FromJson(model), _templates, options?.Culture ?? _culture, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the compiled template against a pre-built <see cref="NgElement"/> context (the
    /// hot-path opt-in, like <see cref="Render(NgElement, TemplateOptions)"/>) and writes the result
    /// to <paramref name="writer"/>. Nothing reaches the writer until the render has fully
    /// succeeded: a throwing render writes zero characters (atomic). What the sink saves is the
    /// final output string.
    /// </summary>
    /// <param name="context">The pre-built data context.</param>
    /// <param name="writer">The sink the rendered output is written to.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="NgSharpException">Same conditions as <see cref="Render(object, TemplateOptions)"/> —
    /// and the writer receives nothing when it throws.</exception>
    public void Render(NgElement context, TextWriter writer, TemplateOptions options = null)
    {
        GuardMode(options);

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _capacityHint = _builder.RenderNodesTo(writer, _nodes, context, _templates, options?.Culture ?? _culture, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict);
    }

    /// <summary>
    /// Renders the compiled template against a pre-built <see cref="NgElement"/> context and writes
    /// the result to <paramref name="writer"/>, awaiting the write: the walk is CPU-bound and
    /// synchronous; the await is the write to your writer (real I/O). Nothing reaches the writer
    /// until the render has fully succeeded: a throwing render writes zero characters (atomic).
    /// What the sink saves is the final output string.
    /// </summary>
    /// <param name="context">The pre-built data context.</param>
    /// <param name="writer">The sink the rendered output is written to.</param>
    /// <param name="options">The render options; null (the default) keeps the compile-time defaults.</param>
    /// <param name="cancellationToken">Checked before the render starts and passed to the writer — an
    /// already-canceled token throws before anything is rendered or written.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="NgSharpException">Same conditions as <see cref="Render(object, TemplateOptions)"/> —
    /// and the writer receives nothing when it throws.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task RenderAsync(NgElement context, TextWriter writer, TemplateOptions options = null, CancellationToken cancellationToken = default)
    {
        GuardMode(options);

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _capacityHint = await _builder.RenderNodesToAsync(writer, _nodes, context, _templates, options?.Culture ?? _culture, _capacityHint, options?.Limits ?? _limits, options?.Strict ?? Strict, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Private methods

    // The one input the render can't honor must refuse loudly, not silently: the dialect shaped the
    // AST at compile, so a CONTRADICTING per-render Mode is a caller bug (null, or the compiled mode
    // itself, lets one options instance serve Compile and Render).
    private void GuardMode(TemplateOptions options)
    {
        if (options?.Mode is not null && ReferenceEquals(options.Mode, Mode) == false)
        {
            throw new NgSharpException(
                $"This template was compiled in {Mode} mode — the dialect is baked in at Compile, so Render can't switch it to {options.Mode}. Compile the template with Mode = TemplateMode.{options.Mode} instead.");
        }
    }

    #endregion
}
