using System.Collections.Generic;

namespace NgSharp.Benchmark;

public class PageModel
{
    public string Title { get; set; }

    public int TotalProducts { get; set; }

    public List<Category> Categories { get; set; }
}
