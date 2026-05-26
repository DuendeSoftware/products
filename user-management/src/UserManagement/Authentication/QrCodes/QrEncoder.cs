// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.UserManagement.Authentication.QrCodes.Internal;

namespace Duende.UserManagement.Authentication.QrCodes;

/// <summary>
/// Encodes text or binary data into a QR Code Model 2 symbol.
/// This is the main public entry point for QR code generation.
/// </summary>
public static class QrEncoder
{
    /// <summary>
    /// Encodes the specified string into a QR code symbol using default options
    /// (ECC level M, auto version selection).
    /// The string is converted to UTF-8 bytes before encoding.
    /// </summary>
    /// <param name="data">The text to encode. Must not be null or empty.</param>
    /// <returns>A <see cref="QrSymbol"/> containing the encoded QR code.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data exceeds the capacity of the largest QR version.
    /// </exception>
    public static QrSymbol Encode(string data) => Encode(data, null);

    /// <summary>
    /// Encodes the specified string into a QR code symbol.
    /// The string is converted to UTF-8 bytes before encoding.
    /// </summary>
    /// <param name="data">The text to encode. Must not be null or empty.</param>
    /// <param name="options">
    /// Encoding parameters. When <see langword="null"/>, defaults are used
    /// (ECC level M, auto version selection).
    /// </param>
    /// <returns>A <see cref="QrSymbol"/> containing the encoded QR code.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data exceeds the capacity of the requested (or largest) QR version.
    /// </exception>
    public static QrSymbol Encode(string data, QrEncodeOptions? options)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException("Data must not be null or empty.", nameof(data));
        }

        return Encode(Encoding.UTF8.GetBytes(data), options);
    }

    /// <summary>
    /// Encodes the specified binary data into a QR code symbol using default options
    /// (ECC level M, auto version selection).
    /// </summary>
    /// <param name="data">The raw bytes to encode. Must not be empty.</param>
    /// <returns>A <see cref="QrSymbol"/> containing the encoded QR code.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data exceeds the capacity of the largest QR version.
    /// </exception>
    public static QrSymbol Encode(ReadOnlySpan<byte> data) => Encode(data, null);

    /// <summary>
    /// Encodes the specified binary data into a QR code symbol.
    /// </summary>
    /// <param name="data">The raw bytes to encode. Must not be empty.</param>
    /// <param name="options">
    /// Encoding parameters. When <see langword="null"/>, defaults are used
    /// (ECC level M, auto version selection).
    /// </param>
    /// <returns>A <see cref="QrSymbol"/> containing the encoded QR code.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data exceeds the capacity of the requested (or largest) QR version.
    /// </exception>
    public static QrSymbol Encode(ReadOnlySpan<byte> data, QrEncodeOptions? options)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Data must not be empty.", nameof(data));
        }

        options ??= new QrEncodeOptions();
        var ecc = options.EccLevel;

        // 1. Detect encoding mode and select version
        var mode = QrModeDetector.Detect(data);
        int version;

        if (options.Version.HasValue)
        {
            version = options.Version.Value;

            if (version < 1 || version > 40)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), version, "Version must be between 1 and 40.");
            }

            // Verify the forced version can hold the data
            var info = QrVersionTables.Get(version, ecc);
            var availableBits = info.DataCodewords * 8;
            var countBits = QrVersionSelector.GetCharacterCountBits(mode, version);
            var dataBits = mode switch
            {
                QrEncodingMode.Numeric =>
                    (data.Length / 3) * 10
                    + (data.Length % 3 == 2 ? 7 : data.Length % 3 == 1 ? 4 : 0),
                QrEncodingMode.Alphanumeric =>
                    (data.Length / 2) * 11 + (data.Length % 2) * 6,
                QrEncodingMode.Byte =>
                    data.Length * 8,
                _ => throw new InvalidOperationException("Unsupported encoding mode."),
            };
            var totalRequired = 4 + countBits + dataBits;

            if (totalRequired > availableBits)
            {
                throw new InvalidOperationException(
                    $"Data requires {totalRequired} bits but version {version} at ECC {ecc} only has {availableBits} bits available.");
            }
        }
        else
        {
            version = QrVersionSelector.SelectVersion(data.Length, ecc, mode);
        }

        // 2. Encode data into codewords
        var dataCodewords = QrDataEncoder.Encode(data, version, ecc);

        // 3. Interleave data blocks and append ECC
        var finalCodewords = QrInterleaver.Interleave(dataCodewords, version, ecc);

        // 4. Build function patterns (finders, timing, alignment, reserved areas)
        var (modules, isFunction) = QrMatrixBuilder.BuildFunctionPatterns(version, ecc);

        // 5. Place data bits in the matrix
        var versionInfo = QrVersionTables.Get(version, ecc);
        QrDataPlacer.PlaceDataBits(modules, isFunction, finalCodewords, versionInfo.RemainderBits);

        // 6. Apply best mask (evaluates all 8 patterns, picks lowest penalty)
        var (maskedModules, maskIndex) = QrMasker.ApplyBestMask(modules, isFunction);

        // 7. Write format info (ECC level + mask pattern) into reserved areas
        QrFormatInfo.WriteFormatInfo(maskedModules, isFunction, ecc, maskIndex);

        // 8. Write version info for versions >= 7
        QrVersionInfoWriter.WriteVersionInfo(maskedModules, isFunction, version);

        return new QrSymbol(version, ecc, maskedModules, isFunction);
    }
}
