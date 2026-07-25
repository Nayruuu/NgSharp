using System;
using System.Collections.Generic;

namespace NgSharp.Parsing;

// The HTML void elements (never a close tag). Extracted from the staged tree-builder when that
// reference pipeline moved to the test assembly — the fused parser keys void-ness on this set.
internal static class HtmlVoidElements
{
    private static readonly HashSet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    public static bool Contains(string name) => All.Contains(name);
}
