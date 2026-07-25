using System;
using System.Collections.Generic;

namespace NgSharp.Benchmark.Realistic;

public sealed class CardList
{
    public string Title { get; set; }

    public string Region { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int TotalImpressions { get; set; }

    public int AvailableCount { get; set; }

    public List<Card> Cards { get; set; }
}
