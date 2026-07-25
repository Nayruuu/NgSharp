using System.Collections.Generic;

namespace NgSharp.Benchmark;

public sealed class Department
{
    public string Name { get; set; }

    public decimal Budget { get; set; }

    public bool IsCore { get; set; }

    public string ThemeColor { get; set; }

    public List<Team> Teams { get; set; }
}
