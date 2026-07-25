using System;

namespace NgSharp.Ast;

// A property/data path ("Name", "product.category.name", "Items[0].Name"). The resolution plan is
// computed once at parse: IsPlain for the single-member common case, else the pre-split Segments.
internal sealed record PathExpression(string Path) : Expression
{
    // Monomorphic inline cache for this access site: NgElement memoizes (type -> resolved accessor) here.
    // Opaque object (NgElement owns the memo type); must stay published as a SINGLE immutable reference
    // (atomic write — never split into multiple fields). Same publication contract as
    // PipeExpression's memo; defended by Concurrency/ConcurrencyStressTests.
    internal object _memberSiteCache;

    // True when the author wrote optional chaining ('a?.b', normalized to 'a.b' by the lexer): the
    // path is EXPLICITLY allowed to be absent, so strict rendering must not throw on its miss.
    public bool Guarded { get; init; }

    public bool IsPlain { get; } = Path.IndexOf('.') < 0 && Path.IndexOf('[') < 0;

    public string[] Segments { get; } =
        Path.IndexOf('.') < 0 && Path.IndexOf('[') < 0
            ? null
            : Path.Replace("[", ".[").Split('.', StringSplitOptions.RemoveEmptyEntries);

    // The cache slot is render-state, not identity — equality must stay on Path alone.
    public bool Equals(PathExpression other) => other is not null && other.Path == Path;

    public override int GetHashCode() => Path.GetHashCode();
}
