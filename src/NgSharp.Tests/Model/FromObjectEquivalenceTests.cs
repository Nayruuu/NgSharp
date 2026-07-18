using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

using NgSharp;
using NgSharp.Pipes;
using NgSharp.Parsing;
using NgSharp.Rendering;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Tests.Model;

// NgElement.FromObject (direct object -> NgElement) must render IDENTICALLY to the JSON round-trip
// (FromJson(Serialize(model))) it replaces — otherwise the object overload changes behaviour.
public class FromObjectEquivalenceTests
{
    private static readonly IReadOnlyDictionary<string, IPipe> NoPipes = new Dictionary<string, IPipe>();
    private static readonly IReadOnlyDictionary<string, IComponent> NoComponents = new Dictionary<string, IComponent>();
    private static readonly IReadOnlyDictionary<string, IDirective> NoDirectives = new Dictionary<string, IDirective>();

    private static string Render(NgElement context, string template)
    {
        var nodes = TemplateParser.ParseDocument(template);
        return HtmlBuilder.MinifyHtml(TemplateRenderer.Render(nodes, context, NoPipes, NoComponents, NoDirectives));
    }

    private static NgElement ViaJson(object model)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(model));
        return NgElement.FromJson(doc.RootElement);
    }

    private static void AssertSame(object model, string template)
        => Assert.Equal(Render(ViaJson(model), template), Render(NgElement.FromObject(model), template));

    [Fact]
    public void Scalars_All_Kinds()
        => AssertSame(
            new { S = "hi", I = 42, L = 9999999999L, D = 3.14, Dec = 2.5m, DecWhole = 20m, B = true, Bf = false, N = (string)null },
            "<p>{{ S }}|{{ I }}|{{ L }}|{{ D }}|{{ Dec }}|{{ DecWhole }}|{{ B }}|{{ Bf }}|{{ N }}</p>");

    [Fact]
    public void Negative_And_Zero_Numbers()
        => AssertSame(new { Neg = -17, Zero = 0, NegD = -3.5 }, "<p>{{ Neg }}|{{ Zero }}|{{ NegD }}</p>");

    [Fact]
    public void DateTime_Value()
        => AssertSame(new { D = new DateTime(2023, 5, 1, 13, 45, 30) }, "<p>{{ D }}</p>");

    [Fact]
    public void Guid_Value()
        => AssertSame(new { G = Guid.Parse("12345678-1234-1234-1234-123456789abc") }, "<p>{{ G }}</p>");

    [Fact]
    public void Enum_Value()
        => AssertSame(new { Day = DayOfWeek.Wednesday }, "<p>{{ Day }}</p>");

    [Fact]
    public void ByteArray_As_Base64()
        => AssertSame(new { Bytes = new byte[] { 1, 2, 3, 250, 255 } }, "<p>{{ Bytes }}</p>");

    [Fact]
    public void Nested_Object_Paths()
        => AssertSame(
            new { User = new { Name = "Alice", Address = new { City = "Paris" } } },
            "<p>{{ User.Name }} - {{ User.Address.City }}</p>");

    [Fact]
    public void Array_With_For_And_If()
        => AssertSame(
            new
            {
                Title = "Catalogue",
                Items = new[]
                {
                    new { Name = "Widget", Price = 10, InStock = true },
                    new { Name = "Gadget", Price = 20, InStock = false }
                }
            },
            "<h1>{{ Title }}</h1><ul><li [for]=\"Items\">{{ Name }}:{{ Price }}<span [if]=\"InStock == true\">*</span></li></ul>");

    [Fact]
    public void Null_Nested_And_Missing_Paths()
        => AssertSame(
            new { A = new { B = (string)null }, Present = "x" },
            "<p>{{ A.B }}|{{ Present }}|{{ Missing }}|{{ A.Missing }}</p>");

    [Fact]
    public void Empty_Array()
        => AssertSame(new { Items = Array.Empty<object>(), Tail = "end" },
            "<ul><li [for]=\"Items\">{{ X }}</li></ul><p>{{ Tail }}</p>");

    [Fact]
    public void Dictionary_As_Object()
        => AssertSame(
            new { Map = new Dictionary<string, object> { ["First"] = "one", ["Second"] = 2 } },
            "<p>{{ Map.First }}|{{ Map.Second }}</p>");

    [Fact]
    public void Comparisons_And_Logical_Over_Both_Paths()
        => AssertSame(
            new { Count = 5, Flag = true, Name = "x" },
            "<p [if]=\"Count > 3 && Flag == true && Name != null\">KEPT</p>");

    [Fact]
    public void Float_Values_Match_The_Json_Path()
        => AssertSame(new { Ratio = 0.1f, Big = 12345.678f, Whole = 5f }, "<p>{{ Ratio }}|{{ Big }}|{{ Whole }}</p>");

    [Fact]
    public void JsonPropertyName_And_JsonIgnore_Are_Honored()
        => AssertSame(
            new AttrModel { FullName = "Alice", Secret = "hidden", Age = 30 },
            "<p>{{ full_name }}|{{ Age }}|{{ Secret }}</p>");

    [Fact]
    public void JsonIgnore_When_Writing_Null()
        => AssertSame(
            new NullIgnoreModel { Keep = "k", Maybe = null },
            "<p>{{ Keep }}|{{ Maybe }}</p>");

    [Fact]
    public void Object_Cycle_Throws_Clean_Instead_Of_StackOverflow()
    {
        var node = new Node { Name = "root" };
        node.Self = node;

        Assert.Throws<InvalidOperationException>(() => NgElement.FromObject(node));
    }

    private class AttrModel
    {
        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        [JsonIgnore]
        public string Secret { get; set; }

        public int Age { get; set; }
    }

    private class NullIgnoreModel
    {
        public string Keep { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Maybe { get; set; }
    }

    private class Node
    {
        public string Name { get; set; }

        public Node Self { get; set; }
    }
}
