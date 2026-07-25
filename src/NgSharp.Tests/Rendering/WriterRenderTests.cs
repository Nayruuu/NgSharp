using System.Text;
using System.Text.Json;
using System.Globalization;

using NgSharp;
using NgSharp.Components;

namespace NgSharp.Tests.Rendering;

// The TextWriter sinks of CompiledTemplate (Render/RenderAsync ×3 model kinds). The contract under
// test: sink output is byte-identical to the string render, the walk stays synchronous (the await is
// the write), and a throwing render is ATOMIC — the writer receives zero characters.
public class WriterRenderTests
{
    // Rich on purpose: pipes (upper/number with arguments), @for with loop variables, [if], a
    // component, and an escaped '&' — the writer path must reproduce every seam byte for byte.
    private const string RichTemplate =
        "<section><h1>{{ Title | upper }}</h1><badge-chip [label]=\"Title\"></badge-chip>"
        + "<ul>@for (p of Products) {<li>{{ $index + 1 }}/{{ $count }} {{ p.Name }}: {{ p.Price | number:'N2' }}<b [if]=\"p.Featured\">*</b></li>}</ul>"
        + "<footer>{{ Total | number:'C2' }}</footer></section>";

    private static readonly object Model = new
    {
        Title = "Quote & Order",
        Products = new[]
        {
            new { Name = "Panel", Price = 1234.5, Featured = true },
            new { Name = "Frame", Price = 99.9, Featured = false },
        },
        Total = 1334.4,
    };

    [Fact]
    public void Writer_Render_Of_An_Object_Model_Is_Byte_Identical_To_The_String_Render()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        var expected = compiled.Render(Model);

        using var sink = new StringWriter();
        compiled.Render(Model, sink);

        Assert.Equal(expected, sink.ToString());
    }

    [Fact]
    public void Writer_Render_Of_A_JsonElement_Model_Is_Byte_Identical_To_The_String_Render()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Model));
        var expected = compiled.Render(json.RootElement);

        using var sink = new StringWriter();
        compiled.Render(json.RootElement, sink);

        Assert.Equal(expected, sink.ToString());
    }

    [Fact]
    public void Writer_Render_Of_A_Prebuilt_NgElement_Is_Byte_Identical_To_The_String_Render()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        var context = NgElement.FromObject(Model);
        var expected = compiled.Render(context);

        using var sink = new StringWriter();
        compiled.Render(context, sink);

        Assert.Equal(expected, sink.ToString());
    }

    [Fact]
    public void Writer_Render_Honors_The_Options_Culture()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        var options = new TemplateOptions { Culture = new CultureInfo("fr-FR") };
        var expected = compiled.Render(Model, options);

        using var sink = new StringWriter();
        compiled.Render(Model, sink, options);

        Assert.Equal(expected, sink.ToString());
        Assert.Contains("€", sink.ToString());
    }

    [Fact]
    public void Writer_Render_Exceeding_MaxOutputChars_Throws_And_Writes_Nothing()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }} and enough static text to overflow a tiny cap</p>");
        var options = new TemplateOptions { Limits = new RenderLimits(maxOutputChars: 8) };

        using var sink = new StringWriter();
        var ex = Assert.Throws<NgSharpException>(() => compiled.Render(new { Name = "x" }, sink, options));

        Assert.Contains("Render limit exceeded", ex.Message);
        Assert.Equal(string.Empty, sink.ToString());
    }

    [Fact]
    public void A_Throwing_Strict_Render_Is_Atomic_The_Writer_Receives_Zero_Characters()
    {
        // Static markup and a resolvable interpolation come FIRST — a streaming-mid-walk renderer
        // would have flushed them before the missing path threw. The sink must stay empty.
        var compiled = HtmlBuilder.Create().Compile("<header>{{ Present }}</header><p>{{ Missing.Path }}</p>");

        using var sink = new StringWriter();
        Assert.Throws<NgSharpException>(() => compiled.Render(new { Present = "ok" }, sink, new TemplateOptions { Strict = true }));

        Assert.Equal(string.Empty, sink.ToString());
    }

    [Fact]
    public void Writer_Render_Refuses_A_Contradicting_Mode_And_Writes_Nothing()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>");

        using var sink = new StringWriter();
        var ex = Assert.Throws<NgSharpException>(() => compiled.Render(new { Name = "x" }, sink, new TemplateOptions { Mode = TemplateMode.Text }));

        Assert.Contains("compiled in", ex.Message);
        Assert.Equal(string.Empty, sink.ToString());
    }

    [Fact]
    public void Writer_Render_Throws_On_A_Null_Writer()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>x</p>");

        Assert.Throws<ArgumentNullException>(() => compiled.Render(new { }, (TextWriter)null!));
    }

    [Fact]
    public async Task RenderAsync_Throws_On_A_Null_Writer()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>x</p>");

        await Assert.ThrowsAsync<ArgumentNullException>(() => compiled.RenderAsync(new { }, null!));
    }

    [Fact]
    public async Task RenderAsync_Of_An_Object_Model_Is_Byte_Identical_To_The_String_Render()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        var expected = compiled.Render(Model);

        using var sink = new StringWriter();
        await compiled.RenderAsync(Model, sink);

        Assert.Equal(expected, sink.ToString());
    }

    [Fact]
    public async Task RenderAsync_JsonElement_And_NgElement_Overloads_Match_The_String_Render()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Model));
        var expectedJson = compiled.Render(json.RootElement);
        var context = NgElement.FromObject(Model);
        var expectedContext = compiled.Render(context);

        using var jsonSink = new StringWriter();
        await compiled.RenderAsync(json.RootElement, jsonSink);
        using var contextSink = new StringWriter();
        await compiled.RenderAsync(context, contextSink);

        Assert.Equal(expectedJson, jsonSink.ToString());
        Assert.Equal(expectedContext, contextSink.ToString());
    }

    [Fact]
    public async Task RenderAsync_With_A_PreCanceled_Token_Throws_Before_Any_Write()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>");

        using var sink = new StringWriter();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => compiled.RenderAsync(new { Name = "x" }, sink, cancellationToken: new CancellationToken(canceled: true)));

        Assert.Equal(string.Empty, sink.ToString());
    }

    [Fact]
    public async Task RenderAsync_Truly_Suspends_When_The_Writer_Yields()
    {
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        var expected = compiled.Render(Model);
        await using var sink = new YieldingWriter();

        await compiled.RenderAsync(Model, sink);

        Assert.True(sink.YieldedBeforeWriting);
        Assert.Equal(expected, sink.ToString());
    }

    [Fact]
    public async Task Text_Mode_Renders_Through_The_Sink_Sync_And_Async()
    {
        var options = new TemplateOptions { Mode = TemplateMode.Text };
        var compiled = HtmlBuilder.Create().Compile("{\"name\":{{ Name | json }},\"qty\":{{ Qty }}}", options);
        var model = new { Name = "Ada & Co", Qty = 3 };
        var expected = compiled.Render(model);

        using var sink = new StringWriter();
        compiled.Render(model, sink);
        using var asyncSink = new StringWriter();
        await compiled.RenderAsync(model, asyncSink);

        Assert.Equal(expected, sink.ToString());
        Assert.Equal(expected, asyncSink.ToString());
    }

    [Fact]
    public void A_StringWriter_Format_Provider_Never_Shapes_The_Output()
    {
        // Formatting happens during the walk (options culture); the sink only receives finished
        // chars — a writer built with a different culture must not change a single byte.
        var compiled = CreateRichBuilder().Compile(RichTemplate);
        var options = new TemplateOptions { Culture = new CultureInfo("fr-FR") };
        var expected = compiled.Render(Model, options);

        using var sink = new StringWriter(new CultureInfo("de-DE"));
        compiled.Render(Model, sink, options);

        Assert.Equal(expected, sink.ToString());
    }

    private static HtmlBuilder CreateRichBuilder()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<BadgeChipComponent>();

        return builder;
    }

    private sealed class BadgeChipComponent : IComponent
    {
        public string ComponentName => "badge-chip";

        public string? Label { get; set; }

        public string Render() => $"<span class=\"chip\">{System.Net.WebUtility.HtmlEncode(Label)}</span>";
    }

    // A writer whose async write REALLY suspends (Task.Yield) — proves RenderAsync rides the writer's
    // asynchrony instead of completing synchronously or blocking.
    private sealed class YieldingWriter : TextWriter
    {
        private readonly StringBuilder _content = new StringBuilder();

        public bool YieldedBeforeWriting { get; private set; }

        public override Encoding Encoding => Encoding.Unicode;

        public override void Write(char value) => _content.Append(value);

        public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            YieldedBeforeWriting = true;
            _content.Append(buffer);
        }

        public override string ToString() => _content.ToString();
    }
}
