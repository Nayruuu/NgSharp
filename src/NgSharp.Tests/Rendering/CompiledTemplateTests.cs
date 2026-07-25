using System;
using System.Collections.Concurrent;

using NgSharp;

namespace NgSharp.Tests.Rendering;

// HtmlBuilder.Compile parses the template AST once; the returned CompiledTemplate renders it many
// times. Must match BuildFromTemplate and stay correct under concurrency (immutable AST + stateless render).
public class CompiledTemplateTests
{
    [Fact]
    public void Compiled_Render_Matches_BuildFromTemplate()
    {
        const string tpl = "<ul><li [for]=\"Items\">{{ Name | upper }}<span [if]=\"Active == true\">*</span></li></ul>";
        var model = new { Items = new[] { new { Name = "a", Active = true }, new { Name = "b", Active = false } } };

        var builder = HtmlBuilder.Create();
        var direct = builder.BuildFromTemplate(tpl, model);
        var compiled = builder.Compile(tpl).Render(model);

        Assert.Equal(direct, compiled);
    }

    [Fact]
    public void Compiled_Template_Renders_Multiple_Models()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>");

        Assert.Contains("<p>Alice</p>", compiled.Render(new { Name = "Alice" }));
        Assert.Contains("<p>Bob</p>", compiled.Render(new { Name = "Bob" }));
    }

    [Fact]
    public void Compiled_Render_Accepts_A_Prebuilt_NgElement()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>");

        var content = compiled.Render(NgElement.FromObject(new { Name = "Carol" }));

        Assert.Contains("<p>Carol</p>", content);
    }

    [Fact]
    public void Compile_Empty_Template_Throws()
        => Assert.ThrowsAny<Exception>(() => HtmlBuilder.Create().Compile(""));

    [Fact]
    public void Pipe_Registered_After_A_Failed_Render_Is_Seen_By_The_Same_CompiledTemplate()
    {
        var builder = HtmlBuilder.Create();
        var compiled = builder.Compile("<p>{{ Name | shout }}</p>");
        var model = new { Name = "alice" };

        // First render: the pipe is not registered yet — the miss must NOT be memoized on the AST.
        var ex = Assert.Throws<NgSharpException>(() => compiled.Render(model));
        Assert.Contains("shout", ex.Message);

        builder.RegisterPipe<ShoutPipe>();

        Assert.Contains("<p>ALICE!</p>", compiled.Render(model));
    }

    [Fact]
    public void ReRegistering_A_Pipe_Under_The_Same_Name_Replaces_It_On_The_Same_CompiledTemplate()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe<TagV1Pipe>();
        var compiled = builder.Compile("<p>{{ Name | tag }}</p>");
        var model = new { Name = "alice" };

        // First render resolves and memoizes v1 on the AST site.
        Assert.Contains("<p>v1:alice</p>", compiled.Render(model));

        builder.RegisterPipe<TagV2Pipe>();   // same PipeName — must invalidate the hit memo

        Assert.Contains("<p>v2:alice</p>", compiled.Render(model));
    }

    [Fact]
    public void Compiled_Template_Renders_Consistently_Under_Concurrency()
    {
        var compiled = HtmlBuilder.Create().Compile("<ul><li [for]=\"Items\">{{ Name }}</li></ul>");
        var model = new { Items = new[] { new { Name = "a" }, new { Name = "b" }, new { Name = "c" } } };
        var expected = compiled.Render(model);

        var results = new ConcurrentBag<string>();
        Parallel.For(0, 500, _ => results.Add(compiled.Render(model)));

        Assert.Equal(500, results.Count);
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    private sealed class ShoutPipe : NgSharp.Pipes.IPipe
    {
        public string PipeName => "shout";

        public string Transform(string tagName, NgElement value, string argument)
            => value.GetString()?.ToUpperInvariant() + "!";
    }

    private sealed class TagV1Pipe : NgSharp.Pipes.IPipe
    {
        public string PipeName => "tag";

        public string Transform(string tagName, NgElement value, string argument) => "v1:" + value.GetString();
    }

    private sealed class TagV2Pipe : NgSharp.Pipes.IPipe
    {
        public string PipeName => "tag";

        public string Transform(string tagName, NgElement value, string argument) => "v2:" + value.GetString();
    }
}
