using System;

namespace NgSharp;

/// <summary>
/// Opt-in resource caps for a single render — the guard rail when the <em>template</em> is not yours
/// (tenant-authored, user-uploaded). Pass an instance via <see cref="TemplateOptions.Limits"/> to any
/// render or compile call; exceeding any cap throws
/// <see cref="NgSharpException"/> with a "Render limit exceeded" message. The default —
/// omitting the options, passing null or <see cref="None"/> — enforces nothing and costs nothing.
/// </summary>
/// <remarks>
/// Limits bound a render's resource use (output size, loop width, fragment recursion). They are NOT a
/// sandbox: see the "Untrusted templates" section of the documentation for what they do and do not cover.
/// </remarks>
public sealed class RenderLimits
{
    /// <summary>
    /// No limits — the default. Renders exactly as if no <see cref="RenderLimits"/> were passed: the
    /// renderer checks for this sentinel once at the start and enforces nothing after that.
    /// </summary>
    public static readonly RenderLimits None = new RenderLimits(int.MaxValue, int.MaxValue, int.MaxValue);

    /// <summary>
    /// Maximum number of characters the rendered output may reach. Exceeding it throws — early when
    /// the output buffer grows past the cap, at the latest when the render completes.
    /// </summary>
    public int MaxOutputChars { get; }

    /// <summary>
    /// Maximum number of iterations a single <c>[for]</c> / <c>@for</c> loop may run (per loop, not
    /// cumulative). Checked against the collection's count before the loop starts.
    /// </summary>
    public int MaxLoopIterations { get; }

    /// <summary>
    /// Maximum <c>@render</c> template-fragment nesting depth. Replaces the built-in guard — without
    /// limits, recursion past depth 50 silently renders nothing; with limits, exceeding this cap throws.
    /// </summary>
    public int MaxRenderDepth { get; }

    /// <summary>
    /// Creates a set of render caps. Every parameter must be positive; the defaults are deliberately
    /// generous — roomy for any legitimate document, fatal for a runaway one.
    /// </summary>
    /// <param name="maxOutputChars">Cap on the rendered output length, in chars (default 1,000,000 — ~2 MB of UTF-16).</param>
    /// <param name="maxLoopIterations">Cap on a single loop's iteration count (default 10,000).</param>
    /// <param name="maxRenderDepth">Cap on <c>@render</c> fragment nesting (default 50, the engine's built-in guard).</param>
    public RenderLimits(int maxOutputChars = 1_000_000, int maxLoopIterations = 10_000, int maxRenderDepth = 50)
    {
        if (maxOutputChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOutputChars), maxOutputChars, "Render limits must be positive.");
        }

        if (maxLoopIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLoopIterations), maxLoopIterations, "Render limits must be positive.");
        }

        if (maxRenderDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRenderDepth), maxRenderDepth, "Render limits must be positive.");
        }

        MaxOutputChars = maxOutputChars;
        MaxLoopIterations = maxLoopIterations;
        MaxRenderDepth = maxRenderDepth;
    }
}
