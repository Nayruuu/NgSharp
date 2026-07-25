using System.Collections.Generic;

namespace NgSharp.Benchmark;

public class Category
{
    public string Name { get; set; }

    public int Count { get; set; }

    public List<Product> Products { get; set; }
}
