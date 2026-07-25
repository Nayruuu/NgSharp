using System.Text.Json;
using System.Globalization;

namespace NgSharp.Tests.Rendering;

// TemplateMode.Text: the engine renders non-HTML output (plain-text emails, JSON, CSV…) — raw text
// plus {{ }} interpolations and @if/@else/@for blocks, with NO escaping anywhere. TemplateMode.Html
// stays the default of every overload, byte-identical to the pre-mode behavior.
public class TextModeTests
{
    private static readonly TemplateOptions TextOptions = new TemplateOptions { Mode = TemplateMode.Text };

    #region Raw emission

    [Fact]
    public void Text_Mode_Emits_Interpolated_Values_Raw()
    {
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "Hello {{ Name }}!", new { Name = "<b>Bob & Alice</b>" }, TextOptions);

        Assert.Equal("Hello <b>Bob & Alice</b>!", output);
    }

    [Fact]
    public void Text_Mode_Does_Not_Rewrite_Preescaped_Data()
    {
        // An already-escaped value must come out untouched: &amp; stays &amp;, never &amp;amp;.
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "{{ Note }}", new { Note = "Fish &amp; Chips" }, TextOptions);

        Assert.Equal("Fish &amp; Chips", output);
    }

    [Fact]
    public void Text_Mode_Keeps_Static_Text_Verbatim()
    {
        // Static template text is not escaped either: < > & flow through byte-identical.
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "a < b & c > d: {{ X }}", new { X = 1 }, TextOptions);

        Assert.Equal("a < b & c > d: 1", output);
    }

    #endregion

    #region Pipes

    [Fact]
    public void Number_Pipe_In_Text_Mode_Keeps_The_NonBreaking_Space_Group_Separator()
    {
        // A U+00A0 group separator (fr-FR style) must survive AS the character — &nbsp; would corrupt
        // a text email or a CSV. Pinned via a custom culture so the golden is ICU/NLS-proof.
        var previous = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = "\u00A0";
        CultureInfo.CurrentCulture = culture;

        try
        {
            const string template = "Total: {{ Amount | number: 'N0' }}";
            var model = new { Amount = 1234567 };

            var text = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);
            var html = HtmlBuilder.Create().BuildFromTemplate(template, model);

            Assert.Equal("Total: 1\u00A0234\u00A0567", text);
            Assert.Equal("Total: 1&nbsp;234&nbsp;567", html);   // the HTML contract, unchanged
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Upper_Pipe_In_Text_Mode_Keeps_Its_Value_Raw()
    {
        // upper is a plain IPipe (no span fast path): exercises the raw slow-path emission.
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "{{ Name | upper }}", new { Name = "a&b <c>" }, TextOptions);

        Assert.Equal("A&B <C>", output);
    }

    #endregion

    #region Control-flow blocks

    [Fact]
    public void If_Else_Blocks_Render_In_Text_Mode()
    {
        const string template = "Status: @if (Premium) {VIP} @else {Standard}.";

        var premium = HtmlBuilder.Create().BuildFromTemplate(template, new { Premium = true }, TextOptions);
        var standard = HtmlBuilder.Create().BuildFromTemplate(template, new { Premium = false }, TextOptions);

        Assert.Equal("Status: VIP.", premium);
        Assert.Equal("Status: Standard.", standard);
    }

    [Fact]
    public void Else_If_Chains_Render_In_Text_Mode()
    {
        const string template = "@if (Score >= 90) {A} @else if (Score >= 50) {B} @else {C}";

        Assert.Equal("A", HtmlBuilder.Create().BuildFromTemplate(template, new { Score = 95 }, TextOptions));
        Assert.Equal("B", HtmlBuilder.Create().BuildFromTemplate(template, new { Score = 60 }, TextOptions));
        Assert.Equal("C", HtmlBuilder.Create().BuildFromTemplate(template, new { Score = 10 }, TextOptions));
    }

    [Fact]
    public void For_Block_Renders_In_Text_Mode()
    {
        const string template = "@for (Items) {- {{ Name }}\n}";
        var model = new { Items = new[] { new { Name = "alpha" }, new { Name = "beta" } } };

        var output = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);

        Assert.Equal("- alpha\n- beta\n", output);
    }

    [Fact]
    public void For_Block_With_Named_Variable_Renders_In_Text_Mode()
    {
        const string template = "@for (item of Items) {{{ item.Name }};}";
        var model = new { Items = new[] { new { Name = "a" }, new { Name = "b" } } };

        var output = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);

        Assert.Equal("a;b;", output);
    }

    [Fact]
    public void Nested_Blocks_Render_In_Text_Mode()
    {
        const string template = "@for (Items) {{{ Name }}@if (Active) { (on)}\n}";
        var model = new
        {
            Items = new[] { new { Name = "a", Active = true }, new { Name = "b", Active = false } }
        };

        var output = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);

        Assert.Equal("a (on)\nb\n", output);
    }

    #endregion

    #region Real-world shapes

    [Fact]
    public void Text_Mode_Renders_A_Valid_Json_Template()
    {
        const string template = "{\"name\":\"{{ Name }}\",\"age\":{{ Age }}}";

        var output = HtmlBuilder.Create().BuildFromTemplate(
            template, new { Name = "Alice", Age = 30 }, TextOptions);

        using var document = JsonDocument.Parse(output);
        Assert.Equal("Alice", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(30, document.RootElement.GetProperty("age").GetInt32());
    }

    [Fact]
    public void Literal_At_Sign_Is_Left_Intact_In_Text_Mode()
    {
        // '@' only opens a block as @if/@for/@else + (…) { — addresses and @-words are plain text
        // (@forum almost matches @for, @ifs almost matches @if: the keyword must end the word).
        const string template = "Contact {{ Name }} <user@example.com> via @forum, @ifs or @ any time.";

        var output = HtmlBuilder.Create().BuildFromTemplate(template, new { Name = "Bob" }, TextOptions);

        Assert.Equal("Contact Bob <user@example.com> via @forum, @ifs or @ any time.", output);
    }

    #endregion

    #region Machine formatting (raw interpolations)

    [Fact]
    public void Text_Mode_Writes_Machine_Bools_And_Invariant_Numbers_Whatever_The_Culture()
    {
        // "Your JSON stays JSON": a bare {{ }} must write machine literals — bools lowercase, numbers
        // culture-invariant — or a fr-FR thread would emit True / 3,14 and corrupt the document.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

        try
        {
            const string template = "{\"active\":{{ Active }},\"price\":{{ Price }},\"amount\":{{ Amount }},\"big\":{{ Big }}}";
            // FromObject ingests decimal as double (the documented model contract) — 19.90m emits 19.9.
            var model = new { Active = true, Price = 3.14, Amount = 19.90m, Big = 9007199254L };

            var output = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);

            Assert.Equal("{\"active\":true,\"price\":3.14,\"amount\":19.9,\"big\":9007199254}", output);

            using var document = JsonDocument.Parse(output);
            Assert.True(document.RootElement.GetProperty("active").GetBoolean());
            Assert.Equal(3.14, document.RootElement.GetProperty("price").GetDouble());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Html_Mode_Keeps_Culture_Formatting_For_Bools_And_Numbers()
    {
        // The HTML contract does NOT move: current culture, StringBuilder parity ("True", fr-FR "3,14").
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

        try
        {
            var output = HtmlBuilder.Create().BuildFromTemplate(
                "<p>{{ Active }} {{ Price }}</p>", new { Active = true, Price = 3.14 });

            Assert.Equal("<p>True 3,14</p>", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    #endregion

    #region json pipe

    [Fact]
    public void Json_Pipe_Quotes_And_Escapes_Strings()
    {
        // The pipe emits the COMPLETE JSON literal — quotes included, specials escaped.
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "{{ Name | json }}", new { Name = "He said \"hi\" \\ once\nbye\ttab" }, TextOptions);

        Assert.Equal("\"He said \\\"hi\\\" \\\\ once\\nbye\\ttab\"", output);
    }

    [Fact]
    public void Json_Pipe_Formats_Numbers_Invariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

        try
        {
            var output = HtmlBuilder.Create().BuildFromTemplate(
                "{{ Price | json }}", new { Price = 1234.5 }, TextOptions);

            Assert.Equal("1234.5", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Json_Pipe_Writes_Lowercase_Bools_And_Null()
    {
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "{{ A | json }}/{{ B | json }}/{{ C | json }}",
            new { A = true, B = false, C = (string)null }, TextOptions);

        Assert.Equal("true/false/null", output);
    }

    [Fact]
    public void Json_Template_Built_With_The_Json_Pipe_Reparses()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

        try
        {
            const string template = "{\"name\":{{ Name | json }},\"price\":{{ Price | json }},\"vip\":{{ Vip | json }},\"note\":{{ Note | json }}}";
            var model = new { Name = "line1\n\"quoted\"", Price = 12.5, Vip = true, Note = (string)null };

            var output = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);

            using var document = JsonDocument.Parse(output);
            Assert.Equal("line1\n\"quoted\"", document.RootElement.GetProperty("name").GetString());
            Assert.Equal(12.5, document.RootElement.GetProperty("price").GetDouble());
            Assert.True(document.RootElement.GetProperty("vip").GetBoolean());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("note").ValueKind);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    #endregion

    #region Untypable block sentinels

    [Fact]
    public void If_Expression_With_Double_Quotes_Renders_Without_Marker_Leak()
    {
        // The block markers are control-character sentinels carrying the expression VERBATIM — a
        // double quote inside it can no longer break an attribute round-trip and leak the marker.
        const string template = "@if (Name == \"Bob\") {hi} bye";

        var match = HtmlBuilder.Create().BuildFromTemplate(template, new { Name = "Bob" }, TextOptions);
        var miss = HtmlBuilder.Create().BuildFromTemplate(template, new { Name = "Ann" }, TextOptions);

        Assert.Equal("hi bye", match);
        Assert.Equal(" bye", miss);
        Assert.DoesNotContain("ng-container", match);
    }

    [Fact]
    public void If_Expression_With_Single_Quotes_Renders_In_Text_Mode()
    {
        const string template = "@if (Name == 'Bob') {hi} bye";

        Assert.Equal("hi bye", HtmlBuilder.Create().BuildFromTemplate(template, new { Name = "Bob" }, TextOptions));
        Assert.Equal(" bye", HtmlBuilder.Create().BuildFromTemplate(template, new { Name = "Ann" }, TextOptions));
    }

    [Fact]
    public void Quoted_Expressions_Work_Inside_A_For_Body()
    {
        const string template = "@for (item of Items) {@if (item.Name == \"a\") {[{{ item.Name }}]}}";
        var model = new { Items = new[] { new { Name = "a" }, new { Name = "b" } } };

        var output = HtmlBuilder.Create().BuildFromTemplate(template, model, TextOptions);

        Assert.Equal("[a]", output);
    }

    [Fact]
    public void Literal_Ng_Container_Markup_Stays_Verbatim_In_Text_Mode()
    {
        // Author-written marker lookalikes are just characters in text mode — they must never execute
        // (X is false: if this "block" ran, "shown" would disappear).
        const string template = "@if (Vip) {*} <ng-container [if]=\"X\">shown</ng-container>";

        var output = HtmlBuilder.Create().BuildFromTemplate(
            template, new { Vip = true, X = false }, TextOptions);

        Assert.Equal("* <ng-container [if]=\"X\">shown</ng-container>", output);
    }

    [Fact]
    public void Literal_Close_Container_Does_Not_Terminate_A_Text_Block()
    {
        // A literal </ng-container> inside a block body used to end the block early — now it is text.
        const string template = "@if (Ok) {a</ng-container>b} done";

        var output = HtmlBuilder.Create().BuildFromTemplate(template, new { Ok = true }, TextOptions);

        Assert.Equal("a</ng-container>b done", output);
    }

    [Fact]
    public void Html_Comment_Between_If_And_Else_Stays_Verbatim_In_Text_Mode()
    {
        // <!-- --> is not a text-mode concept: it does not bridge the @else interstice (HTML mode
        // still skips comments there) — the comment AND the orphaned @else stay literal text.
        const string template = "@if (X) {a} <!-- note --> @else {b}";

        var whenTrue = HtmlBuilder.Create().BuildFromTemplate(template, new { X = true }, TextOptions);
        var whenFalse = HtmlBuilder.Create().BuildFromTemplate(template, new { X = false }, TextOptions);

        Assert.Equal("a <!-- note --> @else {b}", whenTrue);
        Assert.Equal(" <!-- note --> @else {b}", whenFalse);
    }

    [Fact]
    public void Brace_Escape_Hatch_Renders_A_Literal_Closing_Brace()
    {
        // The documented escape hatch for an unpaired brace in static text: {{ '}' }}.
        var output = HtmlBuilder.Create().BuildFromTemplate(
            "@if (Ok) {open} then {{ '}' }} alone", new { Ok = true }, TextOptions);

        Assert.Equal("open then } alone", output);
    }

    [Fact]
    public void Interpolation_Glued_To_A_Block_Brace_Reads_As_A_Literal_Brace_Run()
    {
        // Documented pitfall: @if (x) {{ X }} parses the {{ as block-open + literal brace — write
        // @if (x) { {{ X }} } to interpolate inside a block.
        var glued = HtmlBuilder.Create().BuildFromTemplate(
            "@if (Ok) {{ X }}", new { Ok = true, X = 42 }, TextOptions);
        var spaced = HtmlBuilder.Create().BuildFromTemplate(
            "@if (Ok) { {{ X }} }", new { Ok = true, X = 42 }, TextOptions);

        Assert.Equal("{ X }", glued);
        Assert.Equal(" 42 ", spaced);
    }

    #endregion

    #region Compile + mode plumbing

    [Fact]
    public void Compiled_Text_Template_Renders_Many_Times()
    {
        var compiled = HtmlBuilder.Create().Compile("Hi {{ Name }} & bye", TextOptions);

        Assert.Same(TemplateMode.Text, compiled.Mode);
        Assert.Equal("Hi <A> & bye", compiled.Render(new { Name = "<A>" }));
        Assert.Equal("Hi B&B & bye", compiled.Render(new { Name = "B&B" }));
    }

    [Fact]
    public void Compile_Defaults_To_Html_Mode()
    {
        var compiled = HtmlBuilder.Create().Compile("<p>{{ Name }}</p>");

        Assert.Same(TemplateMode.Html, compiled.Mode);
        Assert.Equal("<p>&lt;b&gt;</p>", compiled.Render(new { Name = "<b>" }));
    }

    [Fact]
    public void Default_Mode_Stays_Html_With_Identical_Output()
    {
        const string template = "<p title=\"x\">{{ Name }} @if (Ok) {yes}</p>";
        var model = new { Name = "<i>", Ok = true };

        var builder = HtmlBuilder.Create();
        var withoutMode = builder.BuildFromTemplate(template, model);
        var withHtml = builder.BuildFromTemplate(template, model, new TemplateOptions { Mode = TemplateMode.Html });
        var withNull = builder.BuildFromTemplate(template, model, (TemplateOptions)null);

        Assert.Equal(withoutMode, withHtml);
        Assert.Equal(withoutMode, withNull);   // null mode falls back to Html
        Assert.Contains("&lt;i&gt;", withoutMode);   // the HTML escaping contract still applies by default
    }

    [Fact]
    public void Text_Mode_Applies_On_All_Three_Ingestion_Paths()
    {
        const string template = "Hello {{ Name }} @if (Vip) {*}";
        const string expected = "Hello <X> *";

        var builder = HtmlBuilder.Create();
        var model = new { Name = "<X>", Vip = true };

        using var document = JsonDocument.Parse("{\"Name\":\"<X>\",\"Vip\":true}");

        var fromObject = builder.BuildFromTemplate(template, model, TextOptions);
        var fromJson = builder.BuildFromTemplate(template, document.RootElement, TextOptions);
        var fromElement = builder.BuildFromTemplate(template, NgElement.FromObject(model), TextOptions);

        Assert.Equal(expected, fromObject);
        Assert.Equal(expected, fromJson);
        Assert.Equal(expected, fromElement);
    }

    #endregion

    [Fact]
    public void Json_Pipe_Fails_Loud_On_A_Cyclic_Model()
    {
        var node = new CyclicNode { Name = "root" };
        node.Self = node;

        var exception = Assert.Throws<InvalidOperationException>(
            () => HtmlBuilder.Create().BuildFromTemplate("{{ N | json }}", new { N = node }, TextOptions));

        Assert.Contains("depth", exception.Message);
    }

    private sealed class CyclicNode
    {
        public string Name { get; set; }

        public CyclicNode Self { get; set; }
    }
}
