// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// Detects the most efficient QR encoding mode for a given byte sequence.
/// </summary>
internal static class QrModeDetector
{
    /// <summary>
    /// Determines the best QR encoding mode for the specified data.
    /// </summary>
    /// <param name="data">The raw byte data to analyze.</param>
    /// <returns>
    /// <see cref="QrEncodingMode.Numeric"/> if all bytes are ASCII digits,
    /// <see cref="QrEncodingMode.Alphanumeric"/> if all bytes belong to the 45-character
    /// alphanumeric set, or <see cref="QrEncodingMode.Byte"/> otherwise.
    /// </returns>
    internal static QrEncodingMode Detect(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return QrEncodingMode.Numeric;
        }

        var isNumeric = true;
        var isAlphanumeric = true;

        foreach (var b in data)
        {
            if (b is < 0x30 or > 0x39) // not '0'-'9'
            {
                isNumeric = false;

                if (!IsAlphanumericChar(b))
                {
                    isAlphanumeric = false;
                    break;
                }
            }
        }

        if (isNumeric)
        {
            return QrEncodingMode.Numeric;
        }

        if (isAlphanumeric)
        {
            return QrEncodingMode.Alphanumeric;
        }

        return QrEncodingMode.Byte;
    }

    private static bool IsAlphanumericChar(byte b) =>
        b is (>= 0x30 and <= 0x39)  // '0'-'9'
            or (>= 0x41 and <= 0x5A) // 'A'-'Z'
            or 0x20                   // space
            or 0x24                   // '$'
            or 0x25                   // '%'
            or 0x2A                   // '*'
            or 0x2B                   // '+'
            or 0x2D                   // '-'
            or 0x2E                   // '.'
            or 0x2F                   // '/'
            or 0x3A;                  // ':'
}
