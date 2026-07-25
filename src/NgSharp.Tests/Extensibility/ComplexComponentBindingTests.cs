using System.Text.Json;

using NgSharp.Tests.CustomElements;

namespace NgSharp.Tests.Extensibility;

// ConvertValue's complex-type component binding: a FromObject model binds the ORIGINAL live CLR
// value as-is; a FromJson model deserializes the JSON node to the target property type.
public class ComplexComponentBindingTests
{
    private const string Template = "<point-list [Title]=\"Caption\" [Points]=\"Points\"></point-list>";

    [Fact]
    public void Complex_List_Property_Binds_Live_Clr_Value_From_Object_Model()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<PointListComponent>();

        var model = new
        {
            Caption = "grid",
            Points = new List<PointItem>
            {
                // Tag is [JsonIgnore]: it can only survive into the render through the LIVE instance.
                new() { Name = "a", X = 1.5, Y = 2, Tag = "!" },
                new() { Name = "b", X = 3, Y = 4.25 }
            }
        };

        var content = builder.BuildFromTemplate(Template, model);

        Assert.Equal("<ul data-title=\"grid\"><li>a!:1.5,2</li><li>b:3,4.25</li></ul>", content);
    }

    [Fact]
    public void Complex_List_Property_Deserializes_From_Json_Model()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<PointListComponent>();

        var json = JsonSerializer.SerializeToElement(new
        {
            Caption = "grid",
            Points = new[]
            {
                new { Name = "a", X = 1.5, Y = 2.0 },
                new { Name = "b", X = 3.0, Y = 4.25 }
            }
        });

        var content = builder.BuildFromTemplate(Template, json);

        Assert.Equal("<ul data-title=\"grid\"><li>a:1.5,2</li><li>b:3,4.25</li></ul>", content);
    }

    [Fact]
    public void Incompatible_Complex_Value_Leaves_The_Property_Null()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<PointListComponent>();

        // A live CLR object NOT assignable to List<PointItem> must not bind (and must not throw).
        var model = new { Caption = "grid", Points = new { Foo = 1 } };

        var content = builder.BuildFromTemplate(Template, model);

        Assert.Equal("<div>no points</div>", content);
    }

    [Fact]
    public void Scalar_Value_On_Complex_Property_Leaves_The_Property_Null()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<PointListComponent>();

        var model = new { Caption = "grid", Points = 42 };

        var content = builder.BuildFromTemplate(Template, model);

        Assert.Equal("<div>no points</div>", content);
    }

    [Fact]
    public void DateTimeOffset_And_TimeSpan_Properties_Bind_From_A_Clr_Model()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<StampComponent>();

        // Deferred scalar boxes (String kind): they must bind via the raw carrier — the hosted-CLR
        // check only accepts Object/Array kinds, and GetString would never convert back.
        var model = new
        {
            At = new DateTimeOffset(2023, 5, 1, 13, 45, 0, TimeSpan.FromHours(2)),
            Window = new TimeSpan(1, 30, 0)
        };

        var content = builder.BuildFromTemplate("<stamp [At]=\"At\" [Window]=\"Window\"></stamp>", model);

        Assert.Equal("<time>2023-05-01 13:45 +02:00|01:30:00</time>", content);
    }

    [Fact]
    public void Strict_Render_Throws_When_A_Property_Conversion_Fails_Naming_Component_And_Property()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<TypedComponent>();
        var compiled = builder.Compile("<typed [Payload]=\"Data\"></typed>", new TemplateOptions { Strict = true });

        // "not base64!" cannot decode to the byte[] property — strict must fail loudly, not bind null.
        var exception = Assert.Throws<NgSharpException>(() => compiled.Render(new { Data = "not base64!" }));

        Assert.Contains("component 'typed'", exception.Message);
        Assert.Contains("property 'Payload'", exception.Message);
        Assert.Contains("conversion to Byte[] failed", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void NonStrict_Render_Leaves_The_Property_Null_When_Conversion_Fails()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent<TypedComponent>();

        var content = builder.BuildFromTemplate("<typed [Payload]=\"Data\"></typed>", new { Data = "not base64!" });

        Assert.Equal("<div>-1</div>", content);
    }
}
