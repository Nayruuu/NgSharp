using System.Text.Json;

using NgSharp;

namespace NgSharp.Tests.Model;

// Guards the FromObject scalar mapping (including the shape-baked compiled getters) against the FromJson
// path: for every scalar CLR type, the object ingestion path must render byte-identically to the JSON path.
// This covers the numeric/bool/enum/decimal/nullable cases the showcase byte-check doesn't necessarily hit.
public class FromObjectScalarParityTests
{
    private enum Color { Red = 0, Green = 1, Blue = 2 }

    private sealed class Scalars
    {
        public byte B { get; set; }
        public sbyte Sb { get; set; }
        public short S { get; set; }
        public ushort Us { get; set; }
        public int I { get; set; }
        public uint Ui { get; set; }
        public long L { get; set; }
        public ulong Ul { get; set; }
        public float F { get; set; }
        public double D { get; set; }
        public decimal Dec { get; set; }
        public bool Bt { get; set; }
        public bool Bf { get; set; }
        public Color E { get; set; }
        public string Str { get; set; }
        public int? Ni { get; set; }
        public int? NiNull { get; set; }
        public bool? Nb { get; set; }
    }

    private const string Template =
        "<p>{{B}}|{{Sb}}|{{S}}|{{Us}}|{{I}}|{{Ui}}|{{L}}|{{Ul}}|{{F}}|{{D}}|{{Dec}}|" +
        "{{Bt}}|{{Bf}}|{{E}}|{{Str}}|{{Ni}}|{{NiNull}}|{{Nb}}</p>";

    [Fact]
    public void Object_Path_Renders_Identically_To_Json_Path_For_All_Scalar_Types()
    {
        var model = new Scalars
        {
            B = 1, Sb = -2, S = -3, Us = 4, I = -5, Ui = 6, L = -7, Ul = 8,
            F = 1.5f, D = 2.5, Dec = 3.5m, Bt = true, Bf = false, E = Color.Green,
            Str = "hi", Ni = 9, NiNull = null, Nb = true,
        };

        var fromObject = HtmlBuilder.Create().BuildFromTemplate(Template, model);
        var fromJson = HtmlBuilder.Create().BuildFromTemplate(Template, JsonSerializer.SerializeToElement(model));

        Assert.Equal(fromJson, fromObject);
    }

    [Fact]
    public void Bool_Toggles_Read_The_Correct_Kind_Through_The_Cached_Boxes()
    {
        // The bool getter returns shared boxes and AddObjectNode recovers True/False by reference identity —
        // so a [class.x] toggle (which reads the bool KIND, not just the value) must still fire correctly.
        var html = HtmlBuilder.Create().BuildFromTemplate(
            "<i [class.on]=\"Bt\" [class.off]=\"Bf\">x</i>",
            new Scalars { Bt = true, Bf = false });

        Assert.Contains("class=\"on\"", html);
        Assert.DoesNotContain("off", html);
    }
}
