using System;
using System.Collections.Generic;

namespace NgSharp.Benchmark.Realistic;

public sealed class ProductSheet
{
    public string Ref { get; set; }

    public string Name { get; set; }

    public string Format { get; set; }

    public string City { get; set; }

    public string Address { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDigital { get; set; }

    public bool HasLighting { get; set; }

    public bool HasAudienceData { get; set; }

    public bool IsPremium { get; set; }

    public bool NearTransport { get; set; }

    public bool Available { get; set; }

    public int Impressions { get; set; }

    public int Reach { get; set; }

    public decimal Frequency { get; set; }

    public decimal BasePrice { get; set; }

    public decimal PremiumSurcharge { get; set; }

    public List<Spec> Specs { get; set; }

    public List<Slot> Slots { get; set; }

    public string Description { get; set; }
}
