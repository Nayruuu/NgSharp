
using NgSharp;

namespace NgSharp.Tests.Rendering;

// Locks the hand-written bare-'&' scanner (which replaced the compiled entity Regex) on the edge cases the
// old @"&(?!#\d+;|#x[0-9a-fA-F]+;|[a-zA-Z][a-zA-Z0-9]*;)" pattern defined — so the engine stays byte-identical
// while being fully regex-free.
public class EntityEscapingTests
{
    [Theory]
    [InlineData("<p>a &amp; b</p>", "<p>a &amp; b</p>")]       // authored named entity — preserved
    [InlineData("<p>a & b</p>", "<p>a &amp; b</p>")]           // bare '&' — escaped
    [InlineData("<p>&#233;</p>", "<p>&#233;</p>")]             // numeric entity — preserved
    [InlineData("<p>&#x41;</p>", "<p>&#x41;</p>")]             // hex entity (lowercase x) — preserved
    [InlineData("<p>&#X41;</p>", "<p>&amp;#X41;</p>")]         // uppercase X is NOT a valid hex ref — '&' escaped
    [InlineData("<p>&amp no semi</p>", "<p>&amp;amp no semi</p>")]  // missing ';' — not an entity — '&' escaped
    [InlineData("<p>ends with &</p>", "<p>ends with &amp;</p>")]    // '&' at end of run — escaped
    public void Static_Text_Preserves_Entities_And_Escapes_Bare_Ampersands(string template, string expected)
        => Assert.Equal(expected, HtmlBuilder.Create().BuildFromTemplate(template, new { }));
}
