// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes;

/// <summary>
/// A QR code symbol produced by the encoder. The arrays are aliased (not copied)
/// for performance, so immutability is by convention -- callers must not mutate them.
/// Contains the module matrix and metadata needed by renderers to produce output.
/// </summary>
public sealed class QrSymbol
{
    /// <summary>
    /// Initializes a new <see cref="QrSymbol"/>. Only the encoder may construct instances.
    /// </summary>
    /// <param name="version">The QR version (1-40).</param>
    /// <param name="eccLevel">The error correction level used.</param>
    /// <param name="modules">The module matrix (true = dark module).</param>
    /// <param name="isFunction">The function-pattern mask (true = function pattern module).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="version"/> is less than 1 or greater than 40.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modules"/> or <paramref name="isFunction"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the dimensions of <paramref name="modules"/> or <paramref name="isFunction"/>
    /// do not match the expected symbol size.
    /// </exception>
#pragma warning disable CA1814 // Multidimensional arrays are intentional: a QR module matrix is inherently a square 2D grid
    internal QrSymbol(int version, QrEccLevel eccLevel, bool[,] modules, bool[,] isFunction)
#pragma warning restore CA1814
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(isFunction);

        if (version < 1 || version > 40)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version,
                "version must be between 1 and 40 inclusive.");
        }

        var size = 17 + version * 4;

        if (modules.GetLength(0) != size || modules.GetLength(1) != size)
        {
            throw new ArgumentException(
                $"Module matrix must be {size}x{size} for version {version}.", nameof(modules));
        }

        if (isFunction.GetLength(0) != size || isFunction.GetLength(1) != size)
        {
            throw new ArgumentException(
                $"Function mask must be {size}x{size} for version {version}.", nameof(isFunction));
        }

        Version = version;
        EccLevel = eccLevel;
        Size = size;
        Modules = modules;
        IsFunction = isFunction;
    }

    /// <summary>
    /// The QR version of this symbol (1-40). Determines the size and data capacity.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// The error correction level used when encoding this symbol.
    /// </summary>
    public QrEccLevel EccLevel { get; }

    /// <summary>
    /// The side length of the symbol in modules. Computed as <c>17 + Version * 4</c>.
    /// Version 1 = 21 modules, version 40 = 177 modules.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// The module matrix. <see langword="true"/> represents a dark module.
    /// Indexed as <c>Modules[row, col]</c> with row 0 at the top.
    /// </summary>
#pragma warning disable CA1819 // Array property is intentional: the matrix is a fixed-size output, not a mutable collection
#pragma warning disable CA1814 // Multidimensional array is intentional: a QR module matrix is inherently a square 2D grid
    public bool[,] Modules { get; }
#pragma warning restore CA1814
#pragma warning restore CA1819

    /// <summary>
    /// A mask indicating which modules are part of function patterns (finder patterns,
    /// timing patterns, alignment patterns, format info, version info).
    /// <see langword="true"/> means the module is a function pattern and is not data.
    /// </summary>
#pragma warning disable CA1819 // Array property is intentional: the mask is a fixed-size output, not a mutable collection
#pragma warning disable CA1814 // Multidimensional array is intentional: a QR module mask is inherently a square 2D grid
    public bool[,] IsFunction { get; }
#pragma warning restore CA1814
#pragma warning restore CA1819
}
