using System.Text.Json;

namespace NgSharp;

// Lazy (no-store) reads of a JsonElement: members / items are read on demand during render; scalars
// normalize through JsonElementToObject. AOT-clean: no reflection, no codegen.
public readonly partial struct NgElement
{
    // The element plus per-node member/item memos, materialized once on first access. The materialize
    // race under concurrent renders is benign: two threads build identical arrays and one wins.
    private sealed class LazyJsonNode
    {
        public readonly JsonElement Element;

        public JsonProperty[] Members;   // object nodes — filled on first member lookup

        public JsonElement[] Items;      // array nodes — filled on first item access

        public LazyJsonNode(JsonElement element)
        {
            Element = element;
        }
    }

    // Backward scan so a duplicate key resolves to the last one written.
    private static bool JsonMember(LazyJsonNode node, string name, out NgElement member)
    {
        var members = node.Members ??= MaterializeMembers(node.Element);

        for (var k = members.Length - 1; k >= 0; k--)
        {
            if (members[k].NameEquals(name))
            {
                member = MakeLazyJson(members[k].Value);

                return true;
            }
        }

        member = default;

        return false;
    }

    private static JsonProperty[] MaterializeMembers(JsonElement element)
    {
        var count = 0;

        foreach (var property in element.EnumerateObject())
        {
            count++;
        }

        var members = new JsonProperty[count];
        var writeIndex = 0;

        foreach (var property in element.EnumerateObject())
        {
            members[writeIndex++] = property;
        }

        return members;
    }

    private static NgElement JsonItem(LazyJsonNode node, int k)
    {
        // The JsonElement int-indexer is O(k) per call — the memo keeps a [for] over n rows O(n), not O(n²).
        var items = node.Items ??= MaterializeItems(node.Element);

        return MakeLazyJson(items[k]);
    }

    private static JsonElement[] MaterializeItems(JsonElement element)
    {
        var items = new JsonElement[element.GetArrayLength()];
        var writeIndex = 0;

        foreach (var item in element.EnumerateArray())
        {
            items[writeIndex++] = item;
        }

        return items;
    }

    // Component binding: the raw JsonElement behind a JSON-ingested Object/Array node (FromJson path).
    internal bool TryGetJsonElement(out JsonElement element)
    {
        if (_carrier is LazyJsonNode json)
        {
            element = json.Element;

            return true;
        }

        element = default;

        return false;
    }

    internal static NgElement MakeLazyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return new NgElement(element.ValueKind, new LazyJsonNode(element));

            case JsonValueKind.Undefined:
                return default;

            default:
                return new NgElement(element.ValueKind, JsonElementToObject(element));
        }
    }
}
