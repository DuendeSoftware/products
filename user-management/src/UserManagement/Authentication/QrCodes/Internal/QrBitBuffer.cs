// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// A MSB-first bit stream buffer used by the QR encoder to pack mode indicators,
/// character count indicators, and data codewords into a contiguous bit sequence.
/// </summary>
internal sealed class QrBitBuffer
{
    private readonly List<byte> _bytes = [];
    private int _lengthInBits;

    /// <summary>
    /// The current number of bits that have been appended to this buffer.
    /// </summary>
    internal int LengthInBits => _lengthInBits;

    /// <summary>
    /// Appends the lowest <paramref name="numberOfBits"/> bits of <paramref name="value"/>
    /// to this buffer in MSB-first order. For negative values, the two's-complement
    /// bit representation is used.
    /// </summary>
    /// <param name="value">The integer value whose lowest bits are to be appended.</param>
    /// <param name="numberOfBits">The number of bits to append (0-32).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="numberOfBits"/> is less than 0 or greater than 32.
    /// </exception>
    internal void AppendBits(int value, int numberOfBits)
    {
        if (numberOfBits < 0 || numberOfBits > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfBits), numberOfBits,
                "numberOfBits must be between 0 and 32 inclusive.");
        }

        for (var i = numberOfBits - 1; i >= 0; i--)
        {
            var bit = (value >> i) & 1;
            var byteIndex = _lengthInBits / 8;
            var bitIndex = 7 - (_lengthInBits % 8);

            if (byteIndex == _bytes.Count)
            {
                _bytes.Add(0);
            }

            if (bit == 1)
            {
                _bytes[byteIndex] |= (byte)(1 << bitIndex);
            }

            _lengthInBits++;
        }
    }

    /// <summary>
    /// Returns the bit at the specified zero-based index in MSB-first order.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns><see langword="true"/> if the bit is 1; <see langword="false"/> if it is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="LengthInBits"/>.
    /// </exception>
    internal bool GetBit(int index)
    {
        if (index < 0 || index >= _lengthInBits)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                _lengthInBits == 0
                    ? "Buffer is empty."
                    : $"index must be between 0 and {_lengthInBits - 1} inclusive.");
        }

        var byteIndex = index / 8;
        var bitIndex = 7 - (index % 8);
        return ((_bytes[byteIndex] >> bitIndex) & 1) == 1;
    }

    /// <summary>
    /// Returns the buffer contents as a byte array.
    /// If <see cref="LengthInBits"/> is not a multiple of 8, the final byte is
    /// zero-padded on the right (low-order bits).
    /// </summary>
    /// <returns>A new byte array containing the buffered bits.</returns>
    internal byte[] ToByteArray() => [.. _bytes];
}
