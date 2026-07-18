using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using NgSharp;

namespace NgSharp.Tests;

// HtmlBuilder.Compile parses the template AST once; the returned CompiledTemplate renders it many
// times. Must match BuildFromTemplateAsync and stay correct under concurrency (immutable AST + stateless render).
public class CompiledTemplateTests
{
    [Fact]
    public async Task Compiled_Render_Matches_BuildFromTemplateAsync()
    {
        const string tpl = "<ul><li [for]=\"Items\">{{ Name | upper }}<span [if]=\"Active == true\">*</span></li></ul>";
        var model = new { Items = new[] { new { Name = "a", Active = true }, new { Name = "b", Active = false } } };

        var builder = HtmlBuilder.Default;
        var direct = await builder.BuildFromTemplateAsync(tpl, model);
        var compiled = builder.Compile(tpl).Render(model);

        Assert.Equal(direct, compiled);
    }

    [Fact]
    public void Compiled_Template_Renders_Multiple_Models()
    {
        var compiled = HtmlBuilder.Default.Compile("<p>{{ Name }}</p>");

        Assert.Contains("<p>Alice</p>", compiled.Render(new { Name = "Alice" }));
        Assert.Contains("<p>Bob</p>", compiled.Render(new { Name = "Bob" }));
    }

    [Fact]
    public void Compiled_Render_Accepts_A_Prebuilt_NgElement()
    {
        var compiled = HtmlBuilder.Default.Compile("<p>{{ Name }}</p>");

        var content = compiled.Render(NgElement.FromObject(new { Name = "Carol" }));

        Assert.Contains("<p>Carol</p>", content);
    }

    [Fact]
    public void Compile_Empty_Template_Throws()
        => Assert.ThrowsAny<Exception>(() => HtmlBuilder.Default.Compile(""));

    [Fact]
    public void Compiled_Template_Renders_Consistently_Under_Concurrency()
    {
        var compiled = HtmlBuilder.Default.Compile("<ul><li [for]=\"Items\">{{ Name }}</li></ul>");
        var model = new { Items = new[] { new { Name = "a" }, new { Name = "b" }, new { Name = "c" } } };
        var expected = compiled.Render(model);

        var results = new ConcurrentBag<string>();
        Parallel.For(0, 500, _ => results.Add(compiled.Render(model)));

        Assert.Equal(500, results.Count);
        Assert.All(results, r => Assert.Equal(expected, r));
    }
}
