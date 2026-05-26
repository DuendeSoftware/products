// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// Selects the smallest QR code version (1-40) that can encode the given data
/// at the requested error correction level and encoding mode.
/// </summary>
internal static class QrVersionSelector
{
    /// <summary>
    /// Returns the smallest QR version (1-40) whose data capacity can hold the
    /// specified payload when encoded with the given mode and error correction level.
    /// </summary>
    /// <param name="dataLength">The number of characters (or bytes for Byte mode) to encode.</param>
    /// <param name="ecc">The error correction level.</param>
    /// <param name="mode">The encoding mode.</param>
    /// <returns>The minimum QR version number that can hold the data.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data is too large for any QR code version at the given error correction level.
    /// </exception>
    internal static int SelectVersion(int dataLength, QrEccLevel ecc, QrEncodingMode mode)
    {
        for (var version = 1; version <= 40; version++)
        {
            var info = QrVersionTables.Get(version, ecc);
            var availableBits = info.DataCodewords * 8;

            var countBits = GetCharacterCountBits(mode, version);
            var dataBits = GetDataBits(mode, dataLength);
            var totalRequired = 4 + countBits + dataBits; // 4 = mode indicator

            if (totalRequired <= availableBits)
            {
                return version;
            }
        }

        throw new InvalidOperationException("Data too large for any QR code version.");
    }

    /// <summary>
    /// Returns the number of bits used for the character count indicator
    /// for the specified mode and version.
    /// </summary>
    internal static int GetCharacterCountBits(QrEncodingMode mode, int version) =>
        mode switch
        {
            QrEncodingMode.Numeric => version <= 9 ? 10 : version <= 26 ? 12 : 14,
            QrEncodingMode.Alphanumeric => version <= 9 ? 9 : version <= 26 ? 11 : 13,
            QrEncodingMode.Byte => version <= 9 ? 8 : 16,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported encoding mode."),
        };

    private static int GetDataBits(QrEncodingMode mode, int dataLength) =>
        mode switch
        {
            QrEncodingMode.Numeric =>
                (dataLength / 3) * 10
                + (dataLength % 3 == 2 ? 7 : dataLength % 3 == 1 ? 4 : 0),
            QrEncodingMode.Alphanumeric =>
                (dataLength / 2) * 11 + (dataLength % 2) * 6,
            QrEncodingMode.Byte =>
                dataLength * 8,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported encoding mode."),
        };
}
