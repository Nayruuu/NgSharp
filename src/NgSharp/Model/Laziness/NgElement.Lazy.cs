using System.Text.Json;
using System.Collections;
using System.Collections.Generic;

namespace NgSharp;

// Lazy reads of a CLR object graph: members / items / scalars are read on demand during render.
// A lazy node: kind Object/Array, carrier = the live CLR object/collection.
public readonly partial struct NgElement
{
    #region Properties

    private bool IsLazy =>
        (_kind == JsonValueKind.Object || _kind == JsonValueKind.Array)
        && _carrier is not null;

    #endregion

    // Monomorphic inline cache for a template access site (see PathExpression._memberSiteCache):
    // immutable, published by a single atomic reference write; Type alone keys it (one member per site).
    // Same publication contract as PipeExpression's memo; defended by Concurrency/ConcurrencyStressTests.
    private sealed class MemberSiteMemo
    {
        public readonly System.Type Type;

        public readonly BindableProperty Prop;

        public MemberSiteMemo(System.Type type, BindableProperty prop)
        {
            Type = type;
            Prop = prop;
        }
    }

    #region Private methods

    private NgElement ComputedMember(string name)
    {
        if (name == "Count")
        {
            return Number(Count);
        }

        if (name == "Length")
        {
            return Number(Length);
        }

        return default;
    }

    // Per-segment cell of the site cache (dotted paths); published by a single reference store on a hit.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    private NgElement ResolveMemberSited(string name, ref MemberSiteMemo slot)
    {
        var obj = _carrier;

        if (_kind == JsonValueKind.Object && obj is not null
            && obj is not LazyJsonNode && obj is not IDictionary)
        {
            var type = obj.GetType();
            var memo = slot;

            if (memo is null || memo.Type != type)
            {
                if (GetPropertyMap(type).TryGetValue(name, out var prop) == false)
                {
                    return ComputedMember(name);
                }

                memo = new MemberSiteMemo(type, prop);
                slot = memo;
            }

            var value = memo.Prop.Getter(obj);

            if (memo.Prop.SkipWhenNull && value is null)
            {
                // A WhenWritingNull property that IS null behaves as absent.
                return ComputedMember(name);
            }

            return WrapMember(value, memo.Prop.Shape);
        }

        return SelectMember(name);
    }

    // Reads a member of a lazy object (POCO property or IDictionary key); false when absent.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the object's properties.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses compiled property accessors.")]
#endif
    private static bool LazyMember(object obj, string name, out NgElement member)
    {
        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key?.ToString() == name)
                {
                    member = MakeLazy(entry.Value);

                    return true;
                }
            }

            member = default;

            return false;
        }

        if (GetPropertyMap(obj.GetType()).TryGetValue(name, out var prop))
        {
            var value = prop.Getter(obj);
            if (prop.SkipWhenNull && value is null)
            {
                member = default;

                return false;
            }

            member = WrapMember(value, prop.Shape);

            return true;
        }

        member = default;

        return false;
    }

    // A baked Shape means the getter already produced the final store value — wrap with the known kind, no re-normalization.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    private static NgElement WrapMember(object value, ScalarShape shape)
    {
        switch (shape)
        {
            case ScalarShape.Bool:
                return new NgElement(ReferenceEquals(value, BoxedTrue) ? JsonValueKind.True : JsonValueKind.False, value);
            case ScalarShape.Number:
                return new NgElement(JsonValueKind.Number, value);
            case ScalarShape.String:
                return value is null ? Null : new NgElement(JsonValueKind.String, value);
            default:
                return MakeLazy(value);
        }
    }

    // The public Properties view (last-wins on duplicate keys); not on the render hot path.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflects over the object's properties.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses compiled property accessors.")]
#endif
    private static IReadOnlyDictionary<string, NgElement> LazyProperties(object obj)
    {
        var view = new Dictionary<string, NgElement>();

        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                view[entry.Key?.ToString()] = MakeLazy(entry.Value);
            }

            return view;
        }

        var props = GetBindableProperties(obj.GetType());
        for (var i = 0; i < props.Length; i++)
        {
            var value = props[i].Getter(obj);
            if (props[i].SkipWhenNull && value is null)
            {
                continue;
            }

            view[props[i].Name] = WrapMember(value, props[i].Shape);
        }

        return view;
    }

    // MakeLazy guarantees every lazy Array carrier is an IList (a non-IList enumerable is materialized
    // at wrap time), so Count and item reads are O(1) and never re-enumerate the source.
    private static int LazyCount(object collection)
    {
        return ((ICollection)collection).Count;
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads a collection item, possibly a reflected object.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("May read a reflected object.")]
#endif
    private static NgElement LazyItem(object collection, int k)
    {
        return MakeLazy(((IList)collection)[k]);
    }

    // The CLR scalars MakeLazy defers under String kind: the original box is kept and the exact STJ
    // text is produced on demand (Value/GetString), so the object path stays byte-identical to the
    // JSON ingestion path without eager serialization.
    private static bool IsDeferredStjScalar(object value) =>
        value is System.DateTime || value is System.DateTimeOffset
        || value is System.TimeSpan || value is System.Uri || value is System.Version
#if NET6_0_OR_GREATER
        || value is System.DateOnly || value is System.TimeOnly
#endif
        ;

    #endregion

    #region Internal methods

    // Component binding: the live CLR object/collection carried by a lazy Object/Array node (never a LazyJsonNode nor a scalar box).
    internal bool TryGetHostedClrValue(out object value)
    {
        if (IsLazy && _carrier is not LazyJsonNode)
        {
            value = _carrier;

            return true;
        }

        value = null;

        return false;
    }

    // Component binding: the raw carrier whatever the kind — lets a deferred scalar box (DateTimeOffset, TimeSpan, Uri, …) bind as-is; null for JSON-ingested nodes.
    internal object CarrierForBinding => _carrier is LazyJsonNode ? null : _carrier;

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Reads the object with reflection. For trimming / Native AOT build the model from a JsonElement (FromJson) instead.")]
#endif
    internal static NgElement MakeLazy(object value)
    {
        if (value is null)
        {
            return Null;
        }

        // Deferred STJ scalars (DateTime(Offset), TimeSpan, Uri, Version, DateOnly/TimeOnly): the node
        // keeps the ORIGINAL box under String kind — Value materializes the STJ text only on demand,
        // and GetDateTime unboxes a DateTime directly (a boxed TimeSpan never matches its unbox test).
        if (IsDeferredStjScalar(value))
        {
            return new NgElement(JsonValueKind.String, value);
        }

        if (TryScalar(value, out var kind, out var boxed))
        {
            return new NgElement(kind, boxed);
        }

        if (value is IDictionary)
        {
            return new NgElement(JsonValueKind.Object, value);
        }

        if (value is IEnumerable enumerable)
        {
            // IList (arrays, List<T>…) indexes O(1) and wraps as-is, zero cost. Any OTHER enumerable
            // is materialized ONCE here: Count-then-items would re-enumerate it n+1 times (n+1
            // queries for an IQueryable) and a one-shot iterator would silently yield EMPTY items
            // after its first pass.
            if (value is IList)
            {
                return new NgElement(JsonValueKind.Array, value);
            }

            var items = new List<object>();
            foreach (var item in enumerable)
            {
                items.Add(item);
            }

            return new NgElement(JsonValueKind.Array, items);
        }

        return new NgElement(JsonValueKind.Object, value);
    }

    // Only the lazy CLR-POCO form is site-cacheable; every other shape — and every miss — keeps the exact ResolveMember semantics.
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only for values ingested through the RequiresUnreferencedCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only for values ingested through the RequiresDynamicCode-gated object path (FromObject); the JsonElement path dispatches to the AOT-clean Json* helpers first.")]
#endif
    internal NgElement SelectMember(string name, NgSharp.Ast.PathExpression site)
    {
        var obj = _carrier;

        if (_kind == JsonValueKind.Object && obj is not null
            && obj is not LazyJsonNode && obj is not IDictionary)
        {
            var type = obj.GetType();
            var memo = site._memberSiteCache as MemberSiteMemo;

            if (memo is null || memo.Type != type)
            {
                if (GetPropertyMap(type).TryGetValue(name, out var prop) == false)
                {
                    return ComputedMember(name);
                }

                memo = new MemberSiteMemo(type, prop);
                site._memberSiteCache = memo;
            }

            var value = memo.Prop.Getter(obj);

            if (memo.Prop.SkipWhenNull && value is null)
            {
                // A WhenWritingNull property that IS null behaves as absent.
                return ComputedMember(name);
            }

            return WrapMember(value, memo.Prop.Shape);
        }

        return SelectMember(name);
    }

    #endregion
}
