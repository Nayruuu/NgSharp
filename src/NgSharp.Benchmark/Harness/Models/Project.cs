using System.Collections.Generic;

namespace NgSharp.Benchmark;

// One row of the showcase's extended catalogue — each field exists to exercise one V3 built-in pipe:
// Code (pad), Owner (titlecase, stored in capitals), Nickname (default — null on one item),
// Description (truncate), Budget (currency), Tags (join), Status (the @switch branch selector).
public sealed class Project
{
    public int Code { get; set; }

    public string Owner { get; set; }

    public string Nickname { get; set; }

    public string Description { get; set; }

    public decimal Budget { get; set; }

    public string Status { get; set; }

    public List<string> Tags { get; set; }
}
