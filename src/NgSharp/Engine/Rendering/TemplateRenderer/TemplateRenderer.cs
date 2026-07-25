using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Rendering;

// The AST renderer's entry point and node dispatch. Element layer: TemplateRenderer.Elements.cs;
// component/directive/ng-template layer: TemplateRenderer.Components.cs.
internal static partial class TemplateRenderer
{
    public static string Render(
        IReadOnlyList<TemplateNode> nodes,
        NgElement context,
        IReadOnlyDictionary<string, IPipe> pipes = null,
        IReadOnlyDictionary<string, ComponentRegistration> components = null,
        IReadOnlyDictionary<string, IDirective> directives = null,
        IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates = null,
        int initialCapacity = 256,
        RenderLimits limits = null,
        bool strict = false)
    {
        // The single limits check of the default path: None (or null) normalizes to null here, so
        // every enforcement site downstream is a null-test that is never taken by default.
        if (ReferenceEquals(limits, RenderLimits.None))
        {
            limits = null;
        }

        // CompiledTemplate feeds back the previous render's length as the capacity hint.
        var builder = new PooledCharWriter(initialCapacity, limits is null ? int.MaxValue : limits.MaxOutputChars);

        try
        {
            // The strict flag rides the scope chain (read only in resolution-failure branches) — the
            // render loop itself has NO strict tests: default and strict renders share one hot path.
            var scope = new RenderScope(pipes, components, directives, templates, limits, strict);
            scope.EnterScope(context);   // the root frame of the scope chain
            RenderNodes(nodes, context, scope, builder);

            return builder.ToString();
        }
        finally
        {
            builder.Return();
        }
    }

    // The TextWriter twin of Render — same pooled buffer, same walk; the sink only receives the
    // buffer once the walk has fully succeeded (a throwing render writes zero characters). Culture
    // lives HERE (not in CompiledTemplate) so the swap brackets the synchronous walk only; returns
    // the written length, fed back as the caller's capacity hint.
    public static int RenderTo(
        TextWriter sink,
        IReadOnlyList<TemplateNode> nodes,
        NgElement context,
        CultureInfo culture,
        IReadOnlyDictionary<string, IPipe> pipes = null,
        IReadOnlyDictionary<string, ComponentRegistration> components = null,
        IReadOnlyDictionary<string, IDirective> directives = null,
        IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates = null,
        int initialCapacity = 256,
        RenderLimits limits = null,
        bool strict = false)
    {
        if (ReferenceEquals(limits, RenderLimits.None))
        {
            limits = null;
        }

        var builder = new PooledCharWriter(initialCapacity, limits is null ? int.MaxValue : limits.MaxOutputChars);

        try
        {
            using (new CultureSwap(culture))
            {
                var scope = new RenderScope(pipes, components, directives, templates, limits, strict);
                scope.EnterScope(context);
                RenderNodes(nodes, context, scope, builder);
            }

            builder.WriteTo(sink);

            return builder.Length;
        }
        finally
        {
            builder.Return();
        }
    }

    // The async twin of RenderTo. The walk is CPU-bound and stays SYNCHRONOUS; the one await is the
    // drain to the sink (the real I/O). The culture swap is posed and removed around the walk only —
    // never across the await (ExecutionContext would leak the swap to the continuation); the drain
    // formats nothing. No stackalloc lives in this method (they sit in the called walk — legal).
    public static async Task<int> RenderToAsync(
        TextWriter sink,
        IReadOnlyList<TemplateNode> nodes,
        NgElement context,
        CultureInfo culture,
        IReadOnlyDictionary<string, IPipe> pipes = null,
        IReadOnlyDictionary<string, ComponentRegistration> components = null,
        IReadOnlyDictionary<string, IDirective> directives = null,
        IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> templates = null,
        int initialCapacity = 256,
        RenderLimits limits = null,
        bool strict = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ReferenceEquals(limits, RenderLimits.None))
        {
            limits = null;
        }

        var builder = new PooledCharWriter(initialCapacity, limits is null ? int.MaxValue : limits.MaxOutputChars);

        try
        {
            using (new CultureSwap(culture))
            {
                var scope = new RenderScope(pipes, components, directives, templates, limits, strict);
                scope.EnterScope(context);
                RenderNodes(nodes, context, scope, builder);
            }

            await builder.WriteToAsync(sink, cancellationToken).ConfigureAwait(false);

            return builder.Length;
        }
        finally
        {
            builder.Return();
        }
    }

    private static void RenderNodes(IReadOnlyList<TemplateNode> nodes, NgElement context, RenderScope scope, PooledCharWriter builder)
    {
        // Keep the indexed loops (foreach would box the IReadOnlyList enumerator) and the concrete-List
        // fast path (devirtualized indexer) — do not "simplify" this walk.
        if (nodes is List<TemplateNode> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                RenderNode(list[i], context, scope, builder);
            }

            return;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            RenderNode(nodes[i], context, scope, builder);
        }
    }

    // Interpolation pipe fast path, keyed on the RESOLVED instance (a re-registered built-in name bails):
    // an ISpanPipe formats into a stack scratch and streams through the escaper — or straight out for a
    // raw (text-mode) interpolation (a culture's U+00A0 group separator must STAY U+00A0, never &nbsp;).
    // Only "not an ISpanPipe" falls back to the dispatch (nothing evaluated yet — unknown-pipe throw
    // included); once the source is evaluated, a TryTransform decline finishes IN PLACE so the source
    // expression is never evaluated twice. Argument handling mirrors EvaluatePipe.
    private static bool TryAppendSpanPipe(PipeExpression pipe, RenderScope scope, PooledCharWriter builder)
    {
        if (pipe.Resolve(scope.Pipes) is not ISpanPipe spanPipe)
        {
            return false;
        }

        var source = ExpressionEvaluator.Evaluate(pipe.Source, scope.ScopeChain, scope.Pipes);
        var argument = pipe.LiteralArgument
            ?? (pipe.Arguments.Count > 0 ? ExpressionEvaluator.Evaluate(pipe.Arguments[0], scope.ScopeChain, scope.Pipes).GetString() : null);

        Span<char> scratch = stackalloc char[128];
        if (spanPipe.TryTransform(null, source, argument, scratch, out var written))
        {
            HtmlEscaper.AppendEscaped(builder, scratch.Slice(0, written));

            return true;
        }

        // TryTransform declined (e.g. the no-argument date form, or a result wider than the scratch):
        // finish the string path HERE with the already-evaluated source and argument — returning false
        // would re-evaluate the whole expression (observable with a stateful pipe in the source). The
        // pipe is resolved, so unknown-pipe can't apply; exact parity with EvaluatePipe → Text → Value.
        AppendValue(builder, ((IPipe)spanPipe).Transform(null, source, argument));

        return true;
    }

    // The raw (text-mode) twin of the interpolation dispatch — kept apart so the HTML hot path stays
    // branch-free. Pipe output streams unescaped (a culture's U+00A0 group separator must STAY U+00A0,
    // never &nbsp;); the in-place decline path mirrors TryAppendSpanPipe exactly.
    private static void RenderRawInterpolation(InterpolationNode interpolation, RenderScope scope, PooledCharWriter builder)
    {
        if (interpolation.Expression is PipeExpression pipe && pipe.Resolve(scope.Pipes) is ISpanPipe spanPipe)
        {
            var source = ExpressionEvaluator.Evaluate(pipe.Source, scope.ScopeChain, scope.Pipes);
            var argument = pipe.LiteralArgument
                ?? (pipe.Arguments.Count > 0 ? ExpressionEvaluator.Evaluate(pipe.Arguments[0], scope.ScopeChain, scope.Pipes).GetString() : null);

            Span<char> scratch = stackalloc char[128];
            if (spanPipe.TryTransform(null, source, argument, scratch, out var written))
            {
                builder.Append(scratch.Slice(0, written));

                return;
            }

            AppendValueRaw(builder, ((IPipe)spanPipe).Transform(null, source, argument));

            return;
        }

        var value = ExpressionEvaluator.Evaluate(interpolation.Expression, scope.ScopeChain, scope.Pipes);
        AppendValueRaw(builder, value.Value);
    }

    // Numbers/booleans format via TryFormat straight into the buffer and never need escaping; strings
    // (and fallback ToString) take the escaping path. HTML mode keeps the current-culture
    // StringBuilder contract (measured and documented).
    private static void AppendValue(PooledCharWriter builder, object value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                builder.Append(HtmlEscaper.Escape(text));
                break;
            case long longValue:
                builder.Append(longValue);
                break;
            case int intValue:
                builder.Append(intValue);
                break;
            case double doubleValue:
                builder.Append(doubleValue);
                break;
            case decimal decimalValue:
                builder.Append(decimalValue);
                break;
            case bool boolValue:
                builder.Append(boolValue);
                break;
            default:
                builder.Append(HtmlEscaper.Escape(value.ToString()));
                break;
        }
    }

    // Raw values are MACHINE literals: bools lowercase, numbers culture-invariant — a text-mode
    // template targets JSON/CSV/machine formats, where "True" or a fr-FR "3,14" would corrupt the
    // output. Human formatting stays the pipes' job (current culture, unchanged).
    private static void AppendValueRaw(PooledCharWriter builder, object value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                builder.Append(text);
                break;
            case long longValue:
                builder.AppendInvariant(longValue);
                break;
            case int intValue:
                builder.AppendInvariant(intValue);
                break;
            case double doubleValue:
                builder.AppendInvariant(doubleValue);
                break;
            case decimal decimalValue:
                builder.AppendInvariant(decimalValue);
                break;
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                break;
            default:
                builder.Append(value.ToString());
                break;
        }
    }

    private static void RenderNode(TemplateNode node, NgElement context, RenderScope scope, PooledCharWriter builder)
    {
        // Case order = post-fold dispatch frequency (the type-pattern switch is a sequential isinst
        // ladder) — do NOT reorder; TextNode/CommentNode only survive in unfolded trees.
        switch (node)
        {
            case ConstNode constant:
                // Folded static run: already escaped, appended verbatim.
                builder.Append(constant.Text);
                break;

            case InterpolationNode interpolation:
                // One flag read, then two SPECIALIZED worlds — the HTML hot path pays zero raw branches.
                if (interpolation.Raw)
                {
                    RenderRawInterpolation(interpolation, scope, builder);
                    break;
                }

                if (interpolation.Expression is PipeExpression pipeExpression
                    && TryAppendSpanPipe(pipeExpression, scope, builder))
                {
                    break;
                }

                var value = ExpressionEvaluator.Evaluate(interpolation.Expression, scope.ScopeChain, scope.Pipes);
                AppendValue(builder, value.Value);

                break;

            case ElementNode element:
                RenderElement(element, context, scope, builder);
                break;

            case IfNode ifNode:
                var condition = ExpressionEvaluator.Evaluate(ifNode.Condition, scope.ScopeChain, scope.Pipes);
                var conditionTruthy = condition.GetBoolean();
                // Null here is "not a boolean" — the strict flag is only read behind that cold test.
                // Null/undefined values stay silently falsy even in strict (absent-vs-null is the
                // path check's job); only a non-boolean NON-NULL value is the Angular-truthiness trap.
                if (conditionTruthy is null && scope.ScopeChain.Strict
                    && condition.ValueKind != JsonValueKind.Null && condition.IsUndefined == false)
                {
                    ThrowNonBooleanCondition(ifNode.Condition, condition);
                }

                if (conditionTruthy == true)
                {
                    RenderNode(ifNode.Body, context, scope, builder);
                }
                else if (ifNode.Else is not null)
                {
                    RenderNode(ifNode.Else, context, scope, builder);
                }

                break;

            case ForNode forNode:
                var collection = ExpressionEvaluator.Evaluate(forNode.Collection, scope.ScopeChain, scope.Pipes);
                var itemCount = collection.Count;
                if (scope.Limits is not null && itemCount > scope.Limits.MaxLoopIterations)
                {
                    ThrowLoopLimitExceeded(itemCount, scope.Limits.MaxLoopIterations);
                }

                for (var i = 0; i < itemCount; i++)
                {
                    var item = collection.ArrayItem(i);
                    scope.EnterLoopScope(item, forNode.Var, i, itemCount);   // implicit frame ([for]) or named frame (@for (Var of ..))
                    RenderNode(forNode.Body, item, scope, builder);
                    scope.ExitScope();
                }

                break;

            case FragmentNode fragment:
                RenderNodes(fragment.Nodes, context, scope, builder);
                break;

            case NotEmptyNode notEmptyNode:
                var candidate = ExpressionEvaluator.Evaluate(notEmptyNode.Collection, scope.ScopeChain, scope.Pipes);
                if (candidate.Count > 0)
                {
                    RenderNode(notEmptyNode.Body, context, scope, builder);
                }

                break;

            case EmptyNode emptyNode:
                var emptyCandidate = ExpressionEvaluator.Evaluate(emptyNode.Collection, scope.ScopeChain, scope.Pipes);
                if (emptyCandidate.Count == 0)
                {
                    RenderNode(emptyNode.Body, context, scope, builder);
                }

                break;

            case TextNode text:
                builder.Append(HtmlEscaper.EscapeText(text.Text));
                break;

            case CommentNode comment:
                builder.Append("<!--").Append(comment.Text).Append("-->");
                break;

            case ComponentNode component:
                RenderComponent(component, context, scope, builder);
                break;

            // A <ng-template> definition renders nothing where it sits — it's instantiated by @render.
            case TemplateDefNode:
                break;

            case RenderTemplateNode renderTemplate:
                RenderTemplateOutlet(renderTemplate, context, scope, builder);
                break;

            case SwitchNode switchNode:
                RenderSwitch(switchNode, context, scope, builder);
                break;
        }
    }

    // @switch: the value is evaluated ONCE; the first case equal to it (the expressions' '==' rule,
    // ExpressionEvaluator.AreEqual) renders; otherwise @default; otherwise nothing — a matchless
    // switch is never an error, strict included.
    private static void RenderSwitch(SwitchNode switchNode, NgElement context, RenderScope scope, PooledCharWriter builder)
    {
        var value = ExpressionEvaluator.Evaluate(switchNode.Value, scope.ScopeChain, scope.Pipes);

        var cases = switchNode.Cases;
        for (var i = 0; i < cases.Count; i++)
        {
            if (ExpressionEvaluator.AreEqual(value, ExpressionEvaluator.Evaluate(cases[i].Value, scope.ScopeChain, scope.Pipes)))
            {
                RenderNode(cases[i].Body, context, scope, builder);

                return;
            }
        }

        if (switchNode.Default is not null)
        {
            RenderNode(switchNode.Default, context, scope, builder);
        }
    }

    // Out of line so the loop-cap guard in RenderNode stays a compare + a never-taken jump.
    private static void ThrowLoopLimitExceeded(int itemCount, int maxLoopIterations)
        => throw new NgSharpException(
            $"Render limit exceeded: a loop over {itemCount} items is wider than MaxLoopIterations = {maxLoopIterations}.");

    // Out of line so the strict-condition check in RenderNode stays a compare + a never-taken jump.
    private static void ThrowNonBooleanCondition(Expression condition, NgElement value)
        => throw new NgSharpException(
            $"Strict mode: the condition '{ExpressionDescriber.Describe(condition)}' evaluated to a non-boolean (strict truthiness: only real booleans are truthy) — it was a {value.ValueKind}, which non-strict rendering silently treats as false. "
            + "Write an explicit comparison instead (e.g. '!= null', '> 0'), or render without strict.");
}
