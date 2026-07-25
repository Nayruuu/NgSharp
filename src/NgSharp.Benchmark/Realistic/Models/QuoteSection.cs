using System.Collections.Generic;

namespace NgSharp.Benchmark.Realistic;

public sealed class QuoteSection
{
    public string Title { get; set; }

    public string Subtitle { get; set; }

    public bool HasDiscount { get; set; }

    public decimal SubtotalHT { get; set; }

    public int SectionImpressions { get; set; }

    public List<QuoteLine> Lines { get; set; }
}
