using System;

namespace NgSharp.Benchmark.Realistic;

// listes-cartes (card list) archetype: tabular, big flat loop, few pipes.
public sealed class Card
{
    public string Ref { get; set; }

    public string Name { get; set; }

    public string City { get; set; }

    public string Format { get; set; }

    public string Status { get; set; }

    public decimal Price { get; set; }

    public int Impressions { get; set; }

    public DateTime NextSlot { get; set; }

    public bool Available { get; set; }

    public bool Digital { get; set; }

    public bool Promo { get; set; }
}
