using System.Runtime.CompilerServices;

using NgSharp;

namespace NgSharp.Tests.Model;

public class NgElementLayoutTests
{
    [Fact]
    public void NgElement_Fits_In_16_Bytes_For_Register_Returns()
    {
        // The ABI guarantee behind the carrier-union layout: at ≤16 bytes the struct is returned in
        // registers on ARM64 and SysV x64 (Linux/macOS) — Windows x64 uses a hidden return buffer
        // beyond 8 bytes regardless; one field too many and the register targets also silently
        // degrade every SelectMember/ArrayItem/Evaluate return to a memory buffer. Lock it.
        Assert.True(Unsafe.SizeOf<NgElement>() <= 16,
            $"NgElement grew to {Unsafe.SizeOf<NgElement>()} bytes — struct returns fall back to memory beyond 16.");
    }
}
