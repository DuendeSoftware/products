// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.QrCodes.Internal;

namespace Duende.UserManagement.Authentication.QrCodes;

/// <summary>
/// Renders a <see cref="QrSymbol"/> as a PNG image.
/// </summary>
public static class QrPngRenderer
{
    /// <summary>
    /// Renders the QR symbol to a PNG byte array.
    /// </summary>
    /// <param name="symbol">The QR symbol to render.</param>
    /// <param name="moduleSize">The pixel size of each module. Must be at least 1.</param>
    /// <param name="quietZoneModules">
    /// The number of quiet-zone modules around the symbol. When <see langword="null"/>,
    /// defaults to 4 per the ISO 18004 specification.
    /// </param>
    /// <returns>A complete PNG image as a byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="moduleSize"/> is less than 1 or <paramref name="quietZoneModules"/> is negative.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown when the computed image dimensions overflow <see cref="int.MaxValue"/>.
    /// </exception>
    public static byte[] Render(QrSymbol symbol, int moduleSize = 1, int? quietZoneModules = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        if (moduleSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(moduleSize), moduleSize,
                "Module size must be at least 1.");
        }

        var quietZone = quietZoneModules ?? 4;
        if (quietZone < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quietZoneModules), quietZoneModules,
                "Quiet zone must not be negative.");
        }

        // Use checked arithmetic to detect overflow on large symbols/module sizes.
        int totalModules, totalPixels;
        checked
        {
            totalModules = symbol.Size + quietZone * 2;
            totalPixels = totalModules * moduleSize;
        }

        // Build a row-major grayscale pixel array.
        // Dark module = 0x00 (black), light module = 0xFF (white).
        var pixels = new byte[checked(totalPixels * totalPixels)];

        // Fill with white (quiet zone included by default since white is the background).
        Array.Fill(pixels, (byte)0xFF);

        for (var row = 0; row < symbol.Size; row++)
        {
            for (var col = 0; col < symbol.Size; col++)
            {
                if (!symbol.Modules[row, col])
                {
                    continue; // light module - already white
                }

                // Map each dark module to a moduleSize*moduleSize square of black pixels.
                var startY = (quietZone + row) * moduleSize;
                var startX = (quietZone + col) * moduleSize;

                for (var dy = 0; dy < moduleSize; dy++)
                {
                    var pixelRow = startY + dy;
                    var rowOffset = pixelRow * totalPixels;
                    for (var dx = 0; dx < moduleSize; dx++)
                    {
                        pixels[rowOffset + startX + dx] = 0x00;
                    }
                }
            }
        }

        return PngEncoder.Encode(totalPixels, totalPixels, pixels);
    }
}
