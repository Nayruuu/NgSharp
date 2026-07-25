namespace NgSharp.Benchmark.Realistic;

// fiche-produit (product sheet) archetype: conditional-heavy, few loops.
public sealed class Spec
{
    public string Label { get; set; }

    public string Value { get; set; }

    public bool Highlighted { get; set; }
}
