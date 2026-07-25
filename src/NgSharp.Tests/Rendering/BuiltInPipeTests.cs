using System;
using System.Globalization;

using NgSharp;

namespace NgSharp.Tests.Rendering;

// The seven V3.x built-in pipes: default, currency, lower, truncate, join, titlecase, pad.
// Culture-sensitive pipes (currency, titlecase, join) are exercised under fr-FR AND en-US via
// TemplateOptions.Culture; the run itself is pinned to InvariantCulture (TestCulture).
public class BuiltInPipeTests
{
    #region default

    [Fact]
    public void Default_Null_Renders_The_Argument()
        => Assert.Equal("<p>N/A</p>", Render("<p>{{ X | default:'N/A' }}</p>", new { X = (string)null }));

    [Fact]
    public void Default_Missing_Path_Renders_The_Argument()
        => Assert.Equal("<p>N/A</p>", Render("<p>{{ Missing | default:'N/A' }}</p>", new { X = 1 }));

    [Fact]
    public void Default_Empty_String_Renders_The_Argument()
        => Assert.Equal("<p>N/A</p>", Render("<p>{{ X | default:'N/A' }}</p>", new { X = "" }));

    [Fact]
    public void Default_Whitespace_String_Renders_The_Argument()
        => Assert.Equal("<p>N/A</p>", Render("<p>{{ X | default:'N/A' }}</p>", new { X = "   " }));

    [Fact]
    public void Default_False_Is_A_Value_Not_Replaced()
        => Assert.Equal("<p>False</p>", Render("<p>{{ X | default:'N/A' }}</p>", new { X = false }));

    [Fact]
    public void Default_Zero_Is_A_Value_Not_Replaced()
        => Assert.Equal("<p>0</p>", Render("<p>{{ X | default:'N/A' }}</p>", new { X = 0 }));

    #endregion

    #region currency

    [Fact]
    public void Currency_Pins_The_Symbol_Under_Fr_FR()
    {
        var content = Render("<p>{{ Price | currency:'USD' }}</p>", new { Price = 12.5 }, "fr-FR");

        var format = (NumberFormatInfo)new CultureInfo("fr-FR").NumberFormat.Clone();
        format.CurrencySymbol = "$";

        Assert.Equal($"<p>{Escaped(12.5m.ToString("C", format))}</p>", content);
    }

    [Fact]
    public void Currency_Pins_The_Symbol_Under_En_US()
        => Assert.Equal("<p>€12.50</p>", Render("<p>{{ Price | currency:'EUR' }}</p>", new { Price = 12.5 }, "en-US"));

    [Fact]
    public void Currency_Without_Argument_Is_The_Plain_Current_Culture_Format()
        => Assert.Equal("<p>$12.50</p>", Render("<p>{{ Price | currency }}</p>", new { Price = 12.5 }, "en-US"));

    [Fact]
    public void Currency_Unknown_Iso_Code_Becomes_Its_Own_Symbol()
        => Assert.Equal("<p>XYZ12.50</p>", Render("<p>{{ Price | currency:'XYZ' }}</p>", new { Price = 12.5 }, "en-US"));

    [Fact]
    public void Currency_Pins_The_Decimals_With_The_Symbol_Jpy_Has_No_Cents()
    {
        Assert.Equal("<p>¥1,200</p>", Render("<p>{{ Price | currency:'JPY' }}</p>", new { Price = 1200 }, "en-US"));

        var format = (NumberFormatInfo)new CultureInfo("fr-FR").NumberFormat.Clone();
        format.CurrencySymbol = "¥";
        format.CurrencyDecimalDigits = 0;

        Assert.Equal($"<p>{Escaped(1200m.ToString("C", format))}</p>", Render("<p>{{ Price | currency:'JPY' }}</p>", new { Price = 1200 }, "fr-FR"));
    }

    [Fact]
    public void Currency_Unknown_Iso_Code_Keeps_The_Culture_Decimals()
        => Assert.Equal("<p>XYZ12.50</p>", Render("<p>{{ Price | currency:'XYZ' }}</p>", new { Price = 12.5 }, "en-US"));

    [Fact]
    public void Currency_Null_Formats_Zero_Like_The_Number_Pipe()
        => Assert.Equal("<p>$0.00</p>", Render("<p>{{ X | currency:'USD' }}</p>", new { X = (string)null }, "en-US"));

    [Fact]
    public void Currency_Non_Number_Follows_The_Number_Pipe_Contract()
    {
        // NumberPipe throws on a non-numeric value (GetDecimal().Value on a null nullable) — the
        // currency pipe aligns on that exact contract instead of inventing a softer one.
        Assert.ThrowsAny<Exception>(() => Render("<p>{{ X | number:'C' }}</p>", new { X = "abc" }));
        Assert.ThrowsAny<Exception>(() => Render("<p>{{ X | currency:'EUR' }}</p>", new { X = "abc" }));
    }

    #endregion

    #region lower

    [Fact]
    public void Lower_Lowercases_The_Value()
        => Assert.Equal("<p>alice</p>", Render("<p>{{ Name | lower }}</p>", new { Name = "AlIcE" }));

    [Fact]
    public void Lower_Null_Renders_Empty()
        => Assert.Equal("<p></p>", Render("<p>{{ X | lower }}</p>", new { X = (string)null }));

    [Fact]
    public void Lower_Number_Passes_Through_As_Its_String()
        => Assert.Equal("<p>42</p>", Render("<p>{{ N | lower }}</p>", new { N = 42 }));

    [Fact]
    public void Lower_Chained_After_Upper_Wins()
        => Assert.Equal("<p>alice</p>", Render("<p>{{ Name | upper | lower }}</p>", new { Name = "Alice" }));

    #endregion

    #region truncate

    [Fact]
    public void Truncate_Cuts_At_N_Ellipsis_Included()
    {
        var content = Render("<p>{{ X | truncate:10 }}</p>", new { X = "abcdefghijklmnop" });

        Assert.Equal("<p>abcdefghi…</p>", content);
    }

    [Fact]
    public void Truncate_Shorter_Value_Is_Unchanged()
        => Assert.Equal("<p>short</p>", Render("<p>{{ X | truncate:10 }}</p>", new { X = "short" }));

    [Fact]
    public void Truncate_Exact_Length_Is_Unchanged()
        => Assert.Equal("<p>abcde</p>", Render("<p>{{ X | truncate:5 }}</p>", new { X = "abcde" }));

    [Fact]
    public void Truncate_Without_Argument_Defaults_To_Fifty()
    {
        var content = Render("<p>{{ X | truncate }}</p>", new { X = new string('a', 60) });

        Assert.Equal("<p>" + new string('a', 49) + "…</p>", content);
    }

    [Fact]
    public void Truncate_One_Or_Less_Is_Just_The_Ellipsis_When_Cut()
        => Assert.Equal("<p>…</p>", Render("<p>{{ X | truncate:1 }}</p>", new { X = "abc" }));

    [Fact]
    public void Truncate_Null_Renders_Empty()
        => Assert.Equal("<p></p>", Render("<p>{{ X | truncate:10 }}</p>", new { X = (string)null }));

    #endregion

    #region join

    [Fact]
    public void Join_Default_Separator_Is_Comma_Space()
        => Assert.Equal("<p>a, b, c</p>", Render("<p>{{ Tags | join }}</p>", new { Tags = new[] { "a", "b", "c" } }));

    [Fact]
    public void Join_Uses_The_Argument_As_Separator()
        => Assert.Equal("<p>a - b</p>", Render("<p>{{ Tags | join:' - ' }}</p>", new { Tags = new[] { "a", "b" } }));

    [Fact]
    public void Join_Renders_Items_With_The_Current_Culture()
        => Assert.Equal("<p>1,5 - 2,5</p>", Render("<p>{{ Values | join:' - ' }}</p>", new { Values = new[] { 1.5, 2.5 } }, "fr-FR"));

    [Fact]
    public void Join_Non_Collection_Renders_Its_Own_String()
        => Assert.Equal("<p>solo</p>", Render("<p>{{ Name | join }}</p>", new { Name = "solo" }));

    [Fact]
    public void Join_Null_Renders_Empty()
        => Assert.Equal("<p></p>", Render("<p>{{ X | join }}</p>", new { X = (string)null }));

    #endregion

    #region titlecase

    [Fact]
    public void Titlecase_Capitalizes_Each_Word_Lowercasing_The_Rest_En_US()
        => Assert.Equal("<p>Hello World</p>", Render("<p>{{ T | titlecase }}</p>", new { T = "HELLO world" }, "en-US"));

    [Fact]
    public void Titlecase_Uses_The_Current_Culture_Fr_FR()
        => Assert.Equal("<p>Être Humain</p>", Render("<p>{{ T | titlecase }}</p>", new { T = "être HUMAIN" }, "fr-FR"));

    [Fact]
    public void Titlecase_Already_Titled_Is_Unchanged()
        => Assert.Equal("<p>Hello World</p>", Render("<p>{{ T | titlecase }}</p>", new { T = "Hello World" }, "en-US"));

    [Fact]
    public void Titlecase_Null_Renders_Empty()
        => Assert.Equal("<p></p>", Render("<p>{{ X | titlecase }}</p>", new { X = (string)null }));

    #endregion

    #region pad

    [Fact]
    public void Pad_Left_Pads_With_Zeros_To_The_Width()
        => Assert.Equal("<p>000042</p>", Render("<p>{{ Id | pad:6 }}</p>", new { Id = 42 }));

    [Fact]
    public void Pad_Wider_Value_Is_Unchanged()
        => Assert.Equal("<p>1234567</p>", Render("<p>{{ Id | pad:6 }}</p>", new { Id = 1234567 }));

    [Fact]
    public void Pad_Without_Argument_Is_Unchanged()
        => Assert.Equal("<p>42</p>", Render("<p>{{ Id | pad }}</p>", new { Id = 42 }));

    [Fact]
    public void Pad_Works_On_Strings_Too()
        => Assert.Equal("<p>00AB</p>", Render("<p>{{ Code | pad:4 }}</p>", new { Code = "AB" }));

    [Fact]
    public void Pad_Null_Renders_Empty()
        => Assert.Equal("<p></p>", Render("<p>{{ X | pad:6 }}</p>", new { X = (string)null }));

    #endregion

    [Fact]
    public void User_Registration_Overrides_The_Builtin_Default_Pipe()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe<HijackingDefaultPipe>();

        var content = builder.BuildFromTemplate("<p>{{ X | default:'N/A' }}</p>", new { X = (string)null });

        Assert.Equal("<p>hijacked</p>", content);
    }

    private static string Render(string template, object model, string culture = null)
    {
        var options = culture is null ? null : new TemplateOptions { Culture = new CultureInfo(culture) };

        return HtmlBuilder.Create().BuildFromTemplate(template, model, options);
    }

    // Currency output can carry non-breaking separators (U+00A0, escaped to &nbsp; by the renderer;
    // U+202F passes through) — expected strings computed via ToString must take the same trip.
    private static string Escaped(string text) => text.Replace("\u00A0", "&nbsp;");

    private sealed class HijackingDefaultPipe : NgSharp.Pipes.IPipe
    {
        public string PipeName => "default";

        public string Transform(string tagName, NgElement value, string argument) => "hijacked";
    }
}
