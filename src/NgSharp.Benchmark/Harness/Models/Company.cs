using System;
using System.Collections.Generic;

using NgSharp.Pipes;

namespace NgSharp.Benchmark;

public sealed class Company
{
    public string Name { get; set; }

    public string LogoHtml { get; set; }

    // Rendered through the built-in image pipe (data URI on <img>, url(...) elsewhere).
    public ImageData Logo { get; set; }

    // Interpolated inside the rawtext <style> block.
    public string AccentColor { get; set; }

    // Contains a '>' combinator — proves rawtext interpolation is NOT html-escaped.
    public string PrintCss { get; set; }

    public int Headcount { get; set; }

    public DateTime FoundedAt { get; set; }

    // Stored uppercased; the lower pipe restores it in the extended catalogue.
    public string ContactEmail { get; set; }

    public List<Department> Departments { get; set; }

    // The extended catalogue: one project per @switch branch, one null Nickname (default pipe).
    public List<Project> Projects { get; set; }

    // Deliberately empty — the [empty] directive's "no results" message.
    public List<Project> Archived { get; set; }
}
