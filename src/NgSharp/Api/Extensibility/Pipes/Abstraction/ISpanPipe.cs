using System;

namespace NgSharp.Pipes;

// Span fast path for built-in formatting pipes. Internal by design — the public contract stays
// IPipe.Transform, so a custom pipe never enters it. Returning false means "take the exact string
// path instead" and must leave no output in the destination.
internal interface ISpanPipe
{
    bool TryTransform(string tagName, NgElement value, string argument, Span<char> destination, out int written);
}
