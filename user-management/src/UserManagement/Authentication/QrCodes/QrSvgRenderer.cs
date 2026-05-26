// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace Duende.UserManagement.Authentication.QrCodes;

/// <summary>
/// Renders a <see cref="QrSymbol"/> as a self-contained SVG string.
/// </summary>
public static class QrSvgRenderer
{
    /// <summary>
    /// Renders the QR symbol to an SVG string using a single <c>&lt;path&gt;</c> element
    /// for dark modules and a white <c>&lt;rect&gt;</c> background.
    /// </summary>
    /// <param name="symbol">The QR symbol to render.</param>
    /// <param name="moduleSize">The pixel size of each module. Must be at least 1.</param>
    /// <param name="quietZoneModules">
    /// The number of quiet-zone modules around the symbol. When <see langword="null"/>,
    /// defaults to 4 per the ISO 18004 specification.
    /// </param>
    /// <returns>A complete SVG document as a string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="moduleSize"/> is less than 1 or <paramref name="quietZoneModules"/> is negative.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown when the computed dimensions overflow <see cref="int.MaxValue"/>.
    /// </exception>
    public static string Render(QrSymbol symbol, int moduleSize = 1, int? quietZoneModules = null)
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

        // Use checked arithmetic to detect overflow
        int totalModules, totalPixels;
        checked
        {
            totalModules = symbol.Size + quietZone * 2;
            totalPixels = totalModules * moduleSize;
        }

        var sb = new StringBuilder();

        _ = sb.Append(CultureInfo.InvariantCulture,
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {totalPixels} {totalPixels}" shape-rendering="crispEdges">""");

        _ = sb.Append(CultureInfo.InvariantCulture,
            $"""<rect width="{totalPixels}" height="{totalPixels}" fill="#FFFFFF"/>""");

        // Build a single <path> with rectangle sub-paths for all dark modules
        var pathData = new StringBuilder();
        for (var row = 0; row < symbol.Size; row++)
        {
            for (var col = 0; col < symbol.Size; col++)
            {
                if (symbol.Modules[row, col])
                {
                    var x = (quietZone + col) * moduleSize;
                    var y = (quietZone + row) * moduleSize;
                    // M x,y h w v h H x Z - a closed rectangle sub-path
                    _ = pathData.Append(CultureInfo.InvariantCulture, $"M{x},{y}h{moduleSize}v{moduleSize}H{x}Z");
                }
            }
        }

        if (pathData.Length > 0)
        {
            _ = sb.Append(CultureInfo.InvariantCulture, $"""<path d="{pathData}" fill="#000000"/>""");
        }

        _ = sb.Append("</svg>");

        return sb.ToString();
    }
}
