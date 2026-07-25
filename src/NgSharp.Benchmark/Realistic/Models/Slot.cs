using System;

namespace NgSharp.Benchmark.Realistic;

public sealed class Slot
{
    public string Period { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal Price { get; set; }

    public bool Available { get; set; }

    public bool LastMinute { get; set; }
}
