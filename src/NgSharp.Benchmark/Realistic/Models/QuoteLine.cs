using System;

namespace NgSharp.Benchmark.Realistic;

public sealed class QuoteLine
{
    public string Ref { get; set; }

    public string Label { get; set; }

    public string Format { get; set; }

    public string City { get; set; }

    public int Quantity { get; set; }

    public int Impressions { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Discount { get; set; }

    public decimal TotalHT { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool InStock { get; set; }

    public bool OnOption { get; set; }

    public bool Highlighted { get; set; }
}
