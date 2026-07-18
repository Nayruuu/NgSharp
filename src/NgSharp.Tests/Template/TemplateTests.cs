using System.Text.Json;
using System.Collections.Generic;

using NgSharp;
using NgSharp.Template;
using NgSharp.Directives;
using NgSharp.Components;
using NgSharp.Tests.CustomElements;

namespace NgSharp.Tests.Template;

public class TemplateTests
{
    private static NgElement Ctx(object model)
    {
        var json = JsonSerializer.Serialize(model);
        using var doc = JsonDocument.Parse(json);
        return NgElement.FromJson(doc.RootElement.Clone());
    }

    private static string Render(string template, object model = null)
        => TemplateRenderer.Render(TemplateParser.Parse(template), model is null ? null : Ctx(model));

    private static string Render(string template, object model, IReadOnlyDictionary<string, IComponent> components)
        => TemplateRenderer.Render(
            TemplateParser.Parse(template, components?.Keys),
            model is null ? null : Ctx(model),
            null,
            components);

    private static string Render(string template, object model, IReadOnlyDictionary<string, IDirective> directives)
        => TemplateRenderer.Render(
            TemplateParser.Parse(template, null, directives?.Keys),
            model is null ? null : Ctx(model),
            null,
            null,
            directives);

    [Fact]
    public void Renders_A_Static_Element()
        => Assert.Equal("<p>Hello</p>", Render("<p>Hello</p>"));

    [Fact]
    public void Renders_An_Interpolation()
        => Assert.Equal("<p>Alice</p>", Render("<p>{{ Name }}</p>", new { Name = "Alice" }));

    [Fact]
    public void Keeps_The_Text_Around_An_Interpolation()
        => Assert.Equal("<p>Hi Alice!</p>", Render("<p>Hi {{ Name }}!</p>", new { Name = "Alice" }));

    [Fact]
    public void Escapes_Interpolated_Html()
        => Assert.Equal("<p>&lt;b&gt;</p>", Render("<p>{{ X }}</p>", new { X = "<b>" }));

    [Fact]
    public void If_True_Renders_The_Element()
        => Assert.Equal("<p>yes</p>", Render("<p [if]=\"Ok\">yes</p>", new { Ok = true }));

    [Fact]
    public void If_False_Removes_The_Element()
        => Assert.Equal("", Render("<p [if]=\"Ok\">yes</p>", new { Ok = false }));

    [Fact]
    public void If_With_Numeric_Comparison()
        => Assert.Equal("<p>yes</p>", Render("<p [if]=\"Count == 3\">yes</p>", new { Count = 3 }));

    [Fact]
    public void For_Repeats_The_Element_Per_Item()
        => Assert.Equal(
            "<ul><li>a</li><li>b</li></ul>",
            Render("<ul><li [for]=\"Items\">{{ Label }}</li></ul>",
                new { Items = new[] { new { Label = "a" }, new { Label = "b" } } }));

    [Fact]
    public void For_Resolves_Parent_Scope_Paths()
        => Assert.Equal(
            "<li>Z-a</li>",
            Render("<li [for]=\"Items\">{{ Shared.X }}-{{ Label }}</li>",
                new { Items = new[] { new { Label = "a" } }, Shared = new { X = "Z" } }));

    [Fact]
    public void Attr_Directive_Sets_An_Attribute()
        => Assert.Equal(
            "<a href=\"http://y\">x</a>",
            Render("<a [attr.href]=\"Url\">x</a>", new { Url = "http://y" }));

    [Fact]
    public void Attr_Class_Appends_To_The_Existing_Class()
        => Assert.Equal(
            "<span class=\"base hi\">x</span>",
            Render("<span [attr.class]=\"Extra\" class=\"base\">x</span>", new { Extra = "hi" }));

    [Fact]
    public void Style_Directive_Sets_A_Style()
        => Assert.Equal(
            "<p style=\"color: red\">x</p>",
            Render("<p [style.color]=\"C\">x</p>", new { C = "red" }));

    [Fact]
    public void Style_Directive_Appends_To_The_Existing_Style()
        => Assert.Equal(
            "<p style=\"font-weight: bold; color: red\">x</p>",
            Render("<p [style.color]=\"C\" style=\"font-weight: bold\">x</p>", new { C = "red" }));

    [Fact]
    public void Class_Toggle_Adds_The_Class_When_Truthy()
        => Assert.Equal("<p class=\"active\">x</p>", Render("<p [class.active]=\"IsActive\">x</p>", new { IsActive = true }));

    [Fact]
    public void Class_Toggle_Omits_The_Class_When_Falsy()
        => Assert.Equal("<p>x</p>", Render("<p [class.active]=\"IsActive\">x</p>", new { IsActive = false }));

    [Fact]
    public void Class_Toggle_Merges_With_An_Existing_Class()
        => Assert.Equal("<p class=\"base active\">x</p>", Render("<p class=\"base\" [class.active]=\"IsActive\">x</p>", new { IsActive = true }));

    [Fact]
    public void Html_Directive_Injects_Raw_Html()
        => Assert.Equal(
            "<div><b>hi</b></div>",
            Render("<div [html]=\"Body\"></div>", new { Body = "<b>hi</b>" }));

    [Fact]
    public void Component_Property_Decodes_A_Byte_Array()
    {
        var components = new Dictionary<string, IComponent> { ["typed"] = new TypedComponent() };

        var result = Render("<typed [payload]=\"Data\"></typed>", new { Data = new byte[] { 1, 2, 3 } }, components);

        Assert.Equal("<div>3</div>", result);
    }

    [Fact]
    public void Custom_Directive_Is_Applied_Via_The_IDirective_Bridge()
    {
        var directives = new Dictionary<string, IDirective> { ["hidden"] = new HiddenDirective() };

        var result = Render("<span [hidden]=\"IsHidden\">text</span>", new { IsHidden = true }, directives);

        Assert.Equal("<span hidden=\"\">text</span>", result);
    }

    [Fact]
    public void Component_Renders_With_Its_Bound_Properties()
    {
        var components = new Dictionary<string, IComponent>
        {
            ["custom-component"] = new CustomComponent()
        };

        var result = Render(
            "<custom-component [componenttext]=\"Msg\"></custom-component>",
            new { Msg = "hello world" },
            components);

        Assert.Equal("<div>hello world</div>", result);
    }

    [Fact]
    public void At_If_True_Renders_Its_Body_Without_A_Wrapper()
        => Assert.Equal("<p>hi</p>", Render("@if (Ok) {<p>hi</p>}", new { Ok = true }));

    [Fact]
    public void At_If_False_Renders_Nothing()
        => Assert.Equal("", Render("@if (Ok) {<p>hi</p>}", new { Ok = false }));

    [Fact]
    public void At_If_With_A_Comparison()
        => Assert.Equal("<p>hi</p>", Render("@if (Count == 3) {<p>hi</p>}", new { Count = 3 }));

    [Fact]
    public void At_If_Can_Wrap_Bare_Text_Without_An_Element()
        => Assert.Equal("Hello", Render("@if (Ok) {Hello}", new { Ok = true }));

    [Fact]
    public void At_For_Repeats_Its_Body_Without_A_Wrapper()
        => Assert.Equal(
            "<li>a</li><li>b</li>",
            Render("@for (Items) {<li>{{ Label }}</li>}",
                new { Items = new[] { new { Label = "a" }, new { Label = "b" } } }));

    [Fact]
    public void NotEmpty_Renders_When_Collection_Has_Items()
        => Assert.Equal("<div>x</div>", Render("<div [not-empty]=\"Items\">x</div>", new { Items = new[] { 1 } }));

    [Fact]
    public void NotEmpty_Removes_When_Collection_Is_Empty()
        => Assert.Equal("", Render("<div [not-empty]=\"Items\">x</div>", new { Items = new int[0] }));

    [Fact]
    public void NonBreaking_Space_Is_Encoded()
        => Assert.Equal("<p>&nbsp;</p>", Render("<p>&nbsp;</p>"));

    [Fact]
    public void Html_Comments_Are_Preserved()
        => Assert.Equal("<div><!-- note --></div>", Render("<div><!-- note --></div>"));

    [Fact]
    public void Attr_Binding_With_A_Null_Value_Is_Omitted()
        => Assert.Equal("<img>", Render("<img [attr.src]=\"Missing\">", new { }));

    [Fact]
    public void Malformed_Number_Falls_Back_To_Text_Without_Crashing()
        => Assert.Equal("<p>1.2.3</p>", Render("<p>{{ 1.2.3 }}</p>"));

    [Fact]
    public void Style_Content_Is_Not_Html_Escaped()
    {
        var html = TemplateRenderer.Render(
            TemplateParser.ParseDocument("<html><head><style>.a > .b{color:red}</style></head><body></body></html>"), null);

        Assert.Contains("<style>.a > .b{color:red}</style>", html);
    }

    [Fact]
    public void Script_Content_Is_Not_Html_Escaped()
    {
        var html = TemplateRenderer.Render(
            TemplateParser.ParseDocument("<html><body><script>if(a<b){x()}</script></body></html>"), null);

        Assert.Contains("<script>if(a<b){x()}</script>", html);
    }

    [Fact]
    public void Void_Elements_Are_Not_Closed()
        => Assert.Equal(
            "<div><br><img src=\"x\"></div>",
            Render("<div><br><img src=\"x\"></div>"));

    [Fact]
    public void At_Blocks_Nest()
        => Assert.Equal(
            "<li>a</li><li>b</li>",
            Render("@if (Show) {@for (Items) {<li>{{ Label }}</li>}}",
                new { Show = true, Items = new[] { new { Label = "a" }, new { Label = "b" } } }));
}
