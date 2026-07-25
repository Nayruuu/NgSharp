using System;
using System.IO;
using System.Buffers;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;

namespace NgSharp.Rendering;

// Append-only text buffer over a pooled char[]. Not thread-safe — one instance per render; Return()
// gives the buffer back (try/finally in TemplateRenderer.Render). The Append overloads must mirror
// StringBuilder's formatting exactly (TryFormat, null provider = current culture) so output stays byte-identical.
internal sealed class PooledCharWriter
{
    #region Fields

    private char[] _buffer;

    private int _length;

    // int.MaxValue = unlimited (the default): both guard branches below are then never taken, so the
    // hot Append path pays nothing. Enforcement sits in the two cold spots — Grow (a runaway render
    // must grow past any cap) and ToString (catches an overshoot that stayed within the rented buffer).
    private readonly int _maxOutputChars;

    #endregion

    #region Constructors

    public PooledCharWriter(int capacity, int maxOutputChars = int.MaxValue)
    {
        _buffer = ArrayPool<char>.Shared.Rent(capacity < 256 ? 256 : capacity);
        _length = 0;
        _maxOutputChars = maxOutputChars;
    }

    #endregion

    #region Public methods

    public PooledCharWriter Append(char value)
    {
        if (_length >= _buffer.Length)
        {
            Grow(_length + 1);
        }

        _buffer[_length++] = value;

        return this;
    }

    public PooledCharWriter Append(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return this;
        }

        if (_length + value.Length > _buffer.Length)
        {
            Grow(_length + value.Length);
        }

        value.AsSpan().CopyTo(_buffer.AsSpan(_length));
        _length += value.Length;

        return this;
    }

    public PooledCharWriter Append(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return this;
        }

        if (_length + value.Length > _buffer.Length)
        {
            Grow(_length + value.Length);
        }

        value.CopyTo(_buffer.AsSpan(_length));
        _length += value.Length;

        return this;
    }

    public PooledCharWriter Append(int value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public PooledCharWriter Append(long value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public PooledCharWriter Append(double value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public PooledCharWriter Append(decimal value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    // StringBuilder.Append(bool) writes "True"/"False" (invariant); match it.
    public PooledCharWriter Append(bool value) => Append(value ? "True" : "False");

    // Invariant appends — the RAW (text-mode) emission path only: a bare {{ }} in a JSON/CSV template
    // must write machine literals whatever the thread culture. The culture-sensitive Append overloads
    // above stay the HTML contract (StringBuilder parity — measured and documented).
    public PooledCharWriter AppendInvariant(int value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written, default, CultureInfo.InvariantCulture) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public PooledCharWriter AppendInvariant(long value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written, default, CultureInfo.InvariantCulture) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public PooledCharWriter AppendInvariant(double value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written, default, CultureInfo.InvariantCulture) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public PooledCharWriter AppendInvariant(decimal value)
    {
        int written;
        while (value.TryFormat(_buffer.AsSpan(_length), out written, default, CultureInfo.InvariantCulture) == false)
        {
            Grow(_buffer.Length + 1);
        }

        _length += written;

        return this;
    }

    public override string ToString()
    {
        if (_length > _maxOutputChars)
        {
            ThrowOutputLimitExceeded(_length, _maxOutputChars);
        }

        return new string(_buffer, 0, _length);
    }

    // The writer must not be used after Return() (ToString happens first in the caller's try/finally).
    public void Return()
    {
        var toReturn = _buffer;
        _buffer = null;
        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    #endregion

    #region Private methods

    private void Grow(int required)
    {
        // `required` chars are about to be content (the TryFormat retries only reach here with a
        // near-full buffer), so growing past the cap means the output already exceeds it.
        if (required > _maxOutputChars)
        {
            ThrowOutputLimitExceeded(required, _maxOutputChars);
        }

        var newSize = Math.Max(required, _buffer.Length * 2);
        var newBuffer = ArrayPool<char>.Shared.Rent(newSize);
        Array.Copy(_buffer, newBuffer, _length);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    // Out of line so the guard branches stay a compare + a never-taken jump in their callers.
    private static void ThrowOutputLimitExceeded(int required, int maxOutputChars)
        => throw new NgSharpException(
            $"Render limit exceeded: the output needs at least {required} chars, above MaxOutputChars = {maxOutputChars}.");

    #endregion

    #region Internal members

    internal int Length => _length;

    // The sink drains, TextWriter twins of ToString: same cap guard first (an overshoot that stayed
    // within the rented buffer never hit Grow), so a capped render throws before a single char
    // reaches the writer — then the whole buffer in one write.
    internal void WriteTo(TextWriter writer)
    {
        if (_length > _maxOutputChars)
        {
            ThrowOutputLimitExceeded(_length, _maxOutputChars);
        }

        writer.Write(_buffer, 0, _length);
    }

    internal Task WriteToAsync(TextWriter writer, CancellationToken cancellationToken)
    {
        if (_length > _maxOutputChars)
        {
            ThrowOutputLimitExceeded(_length, _maxOutputChars);
        }

        return writer.WriteAsync(_buffer.AsMemory(0, _length), cancellationToken);
    }

    #endregion
}
