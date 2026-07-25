using System;
using System.Text.Json;

namespace NgSharp;

public readonly partial struct NgElement
{
    /// <summary>
    /// Resolves a path against this node: a plain property name, a dotted path, and array indices
    /// (<c>"Items[0].Name"</c>). Falls back to the computed members <see cref="Count"/> /
    /// <see cref="Length"/> when no real property of that name exists.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved node, or null when the path doesn't exist.</returns>
    public NgElement SelectToken(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return default;
        }

        // Plain-name fast path (no '.', no '['): one member scan, no Replace/Split allocation.
        if (path.IndexOf('.') < 0 && path.IndexOf('[') < 0)
        {
            return ResolveMember(this, path);
        }

        return SelectSegments(path.Replace("[", ".[").Split('.', StringSplitOptions.RemoveEmptyEntries));
    }

    // Caller guarantees `name` is a plain member name (no '.', no '[').
    internal NgElement SelectMember(string name) => ResolveMember(this, name);

    // Resolves a pre-split segment plan (see PathExpression).
    internal NgElement SelectSegments(string[] segments) => SelectSegments(segments, 0);

    // As above, but resolving from `start` — used to resolve "p.a.b" against a named @for frame (skip "p").
    internal NgElement SelectSegments(string[] segments, int start)
    {
        var current = this;

        for (var segmentIndex = start; segmentIndex < segments.Length; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            if (segment.StartsWith("[") && segment.EndsWith("]"))
            {
                if (int.TryParse(segment.Trim('[', ']'), out var index) == false)
                {
                    return default;
                }

                if (index < 0 || index >= current.Count)
                {
                    return default;
                }

                current = current.ArrayItem(index);
            }
            else
            {
                current = ResolveMember(current, segment);
                if (current.IsUndefined)
                {
                    return default;
                }
            }
        }

        return current;
    }

    // Site-cached walk: one memo per ABSOLUTE segment position, so the named-frame (start 1) and
    // implicit (start 0) entries share the array consistently; index segments ([n]) leave their slot null.
    internal NgElement SelectSegments(string[] segments, int start, NgSharp.Ast.PathExpression site)
    {
        var memos = site._memberSiteCache as MemberSiteMemo[];
        if (memos is null)
        {
            // A racing first walk may allocate twice; one wins the reference store, the other is garbage.
            memos = new MemberSiteMemo[segments.Length];
            site._memberSiteCache = memos;
        }

        var current = this;

        for (var segmentIndex = start; segmentIndex < segments.Length; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            if (segment.StartsWith("[") && segment.EndsWith("]"))
            {
                if (int.TryParse(segment.Trim('[', ']'), out var index) == false)
                {
                    return default;
                }

                if (index < 0 || index >= current.Count)
                {
                    return default;
                }

                current = current.ArrayItem(index);
            }
            else
            {
                current = current.ResolveMemberSited(segment, ref memos[segmentIndex]);
                if (current.IsUndefined)
                {
                    return default;
                }
            }
        }

        return current;
    }

    // A real data property resolves first; Count/Length are only a fallback, never shadowing data.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    private static NgElement ResolveMember(NgElement element, string name)
    {
        if (element._kind == JsonValueKind.Object && element._carrier is not null)
        {
            if (element._carrier is LazyJsonNode json
                    ? JsonMember(json, name, out var member)
                    : LazyMember(element._carrier, name, out member))
            {
                return member;
            }
        }

        if (name == "Count")
        {
            return Number(element.Count);
        }

        if (name == "Length")
        {
            return Number(element.Length);
        }

        return default;
    }

    private static NgElement Number(int value) => new NgElement(JsonValueKind.Number, value);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Must agree with Equals: hash the normalized value, not the raw box — an int 2 and a
        // double 2.0 are Equal and must hash alike. Object/array/null hash to 0.
        return ValueKind switch
        {
            JsonValueKind.Number => (GetDouble() ?? 0d).GetHashCode(),
            JsonValueKind.String => GetString()?.GetHashCode() ?? 0,
            JsonValueKind.True or JsonValueKind.False => (GetBoolean() ?? false).GetHashCode(),
            _ => 0,
        };
    }

    /// <summary>
    /// Value equality: scalars (string / number / bool / null) compare by value; objects and arrays
    /// compare by node identity (same underlying object).
    /// </summary>
    /// <param name="other">The node to compare with.</param>
    /// <returns><c>true</c> when the two nodes are equal.</returns>
    public bool Equals(NgElement other)
    {
        if (ValueKind != other.ValueKind)
        {
            return false;
        }

        return ValueKind switch
        {
            JsonValueKind.String => GetString() == other.GetString(),
            JsonValueKind.Number => GetDouble() == other.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => GetBoolean() == other.GetBoolean(),
            JsonValueKind.Null => true,
            JsonValueKind.Object => SameNode(other),
            JsonValueKind.Array => SameNode(other),
            _ => Value?.Equals(other.Value) ?? other.Value is null
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is NgElement other && Equals(other);

    private bool SameNode(NgElement other)
    {
        // Lazy CLR nodes wrap the SAME live object, so reference identity holds. Lazy JSON nodes wrap a
        // fresh holder per resolution — whole-object equality is not identity-comparable there (documented limitation).
        if (_carrier is LazyJsonNode mine && other._carrier is LazyJsonNode theirs)
        {
            return ReferenceEquals(mine, theirs);
        }

        return _carrier is not null && ReferenceEquals(_carrier, other._carrier);
    }
}
