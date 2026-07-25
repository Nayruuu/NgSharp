using System;
using System.Collections.Generic;

using NgSharp.Pipes;

namespace NgSharp.Ast;

internal sealed record PipeExpression(Expression Source, string PipeName, IReadOnlyList<Expression> Arguments) : Expression
{
    private PipeMemo _memo;

    // Pre-extracted string form of a single literal argument ({{ x | number:'N2' }}); null when the
    // argument isn't a single literal — the render then evaluates it as an expression.
    public string LiteralArgument { get; } =
        Arguments.Count == 1 && Arguments[0] is LiteralExpression literal ? literal.Value.GetString() : null;

    public IPipe Resolve(IReadOnlyDictionary<string, IPipe> pipes)
    {
        var cached = _memo;
        if (cached is not null && ReferenceEquals(cached.From, pipes))
        {
            return cached.Pipe;
        }

        IPipe resolved = null;
        pipes?.TryGetValue(PipeName, out resolved);

        // Publish the memo ONLY on a hit: a memoized miss would pin "Unknown pipe" onto this AST for
        // the registry's lifetime, even after the pipe gets registered. A miss re-probes the
        // dictionary on every call — the rare path by construction.
        if (resolved is not null)
        {
            _memo = new PipeMemo(pipes, resolved);
        }

        return resolved;
    }

    // The memo slot is render-state, not identity — equality must stay on the structural data alone.
    public bool Equals(PipeExpression other) =>
        other is not null
        && other.PipeName == PipeName
        && Equals(other.Source, Source)
        && Equals(other.Arguments, Arguments)
        && other.LiteralArgument == LiteralArgument;

    public override int GetHashCode() => HashCode.Combine(PipeName, Source, Arguments, LiteralArgument);

    // Memo of the resolved pipe, keyed on the registry instance. Must stay an IMMUTABLE holder published
    // through a single reference write (atomic) — concurrent renders must never observe a torn
    // registry/pipe pair; never split it back into two fields. Same publication contract as
    // NgElement.MemberSiteMemo; defended by Concurrency/ConcurrencyStressTests.
    private sealed class PipeMemo
    {
        public readonly IReadOnlyDictionary<string, IPipe> From;
        public readonly IPipe Pipe;

        public PipeMemo(IReadOnlyDictionary<string, IPipe> from, IPipe pipe)
        {
            From = from;
            Pipe = pipe;
        }
    }
}
