using System.Globalization;

namespace NgSharp;

/// <summary>
/// The options facade of the engine: one immutable record carrying everything a
/// <see cref="HtmlBuilder.BuildFromTemplate(string, object, TemplateOptions)"/>,
/// <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/> or
/// <see cref="CompiledTemplate.Render(object, TemplateOptions)"/> call can tune. Every property is
/// optional; a null property (or a null/omitted options argument) means "use the default".
/// </summary>
/// <remarks>
/// When each field acts:
/// <list type="bullet">
/// <item><description><see cref="Mode"/> — compile-time: picks the dialect the template is parsed in.
/// A <see cref="CompiledTemplate"/> bakes it in, and <see cref="CompiledTemplate.Render(object, TemplateOptions)"/>
/// throws on an explicit Mode contradicting the compiled one.</description></item>
/// <item><description><see cref="Strict"/> — both times: at compile it gates the template through
/// <see cref="HtmlBuilder.Validate(string, TemplateMode)"/> (any error throws), at render it makes a
/// missing model path, a non-boolean non-null condition and a division/modulo by zero throw.</description></item>
/// <item><description><see cref="Culture"/> — render-time: swapped in around the render, restored
/// afterwards. Passed to <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/> it becomes the
/// compiled template's default culture, overridable per render.</description></item>
/// <item><description><see cref="Limits"/> — render-time: resource caps enforced during the render.
/// Passed to <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/> it becomes the compiled
/// template's default caps, overridable per render.</description></item>
/// </list>
/// The record is a pure, immutable DTO: share one instance freely across calls and threads, derive
/// variants with <c>with</c> (e.g. <c>options with { Culture = french }</c>).
/// </remarks>
public sealed record TemplateOptions
{
    /// <summary>
    /// The canonical empty options — every property null, so every call falls back to its defaults
    /// (<see cref="TemplateMode.Html"/>, the builder's strict setting, the ambient culture, no limits).
    /// Passing <see cref="Default"/>, null, or nothing at all are equivalent.
    /// </summary>
    public static readonly TemplateOptions Default = new TemplateOptions();

    /// <summary>
    /// The dialect the template is written in; null defaults to <see cref="TemplateMode.Html"/>.
    /// Compile-time only: a <see cref="CompiledTemplate"/> carries the mode it was compiled with, and
    /// its Render throws <see cref="NgSharpException"/> on an explicit Mode contradicting it (null,
    /// or the compiled mode itself, is fine).
    /// </summary>
    public TemplateMode Mode { get; init; }

    /// <summary>
    /// Opt-in strict mode: the template must pass <see cref="HtmlBuilder.Validate(string, TemplateMode)"/>
    /// without errors before compiling/rendering, and the render throws <see cref="NgSharpException"/>
    /// on a template path missing from the model (instead of rendering empty), on a non-boolean
    /// non-null <c>[if]</c>/<c>@if</c> condition, and on a division/modulo by zero. Null inherits the
    /// builder's strict setting (<see cref="HtmlBuilder.Create(bool)"/>) — or, on
    /// <see cref="CompiledTemplate.Render(object, TemplateOptions)"/>, the compiled template's
    /// <see cref="CompiledTemplate.Strict"/>; an explicit true/false always wins.
    /// </summary>
    public bool? Strict { get; init; }

    /// <summary>
    /// The culture pipes format with: <see cref="CultureInfo.CurrentCulture"/> /
    /// <see cref="CultureInfo.CurrentUICulture"/> are swapped for the duration of the render and
    /// restored afterwards, so one process serves multiple locales without touching thread state.
    /// Null keeps the ambient culture.
    /// </summary>
    public CultureInfo Culture { get; init; }

    /// <summary>
    /// Opt-in resource caps for untrusted templates (see <see cref="RenderLimits"/>), enforced during
    /// the render; exceeding any cap throws <see cref="NgSharpException"/>. Null (or
    /// <see cref="RenderLimits.None"/>) enforces nothing.
    /// </summary>
    public RenderLimits Limits { get; init; }
}
