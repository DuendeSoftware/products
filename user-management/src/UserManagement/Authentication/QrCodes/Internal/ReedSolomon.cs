// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// Reed-Solomon error correction code computation for QR codes.
/// Generates ECC codewords by performing polynomial long division in GF(256).
/// </summary>
internal static class ReedSolomon
{
    private static readonly ConcurrentDictionary<int, byte[]> GeneratorCache = new();

    /// <summary>
    /// Returns the generator polynomial g(x) = ∏(x − α^i) for i = 0 .. eccCount-1.
    /// Coefficients are stored highest-degree first (leading coefficient is always 1).
    /// Results are memoized by <paramref name="eccCount"/>.
    /// </summary>
    internal static byte[] GetGeneratorPolynomial(int eccCount) =>
        GeneratorCache.GetOrAdd(eccCount, static count =>
        {
            // Start with g(x) = 1
            byte[] poly = [1];

            for (var i = 0; i < count; i++)
            {
                // Multiply poly by (x - α^i), i.e., (x + α^i) in GF(2)
                // factor = [1, α^i]
                var alphaI = Gf256.Exp[i];
                var newPoly = new byte[poly.Length + 1];

                for (var j = 0; j < poly.Length; j++)
                {
                    // x term: poly[j] * x  ->  goes into newPoly[j]
                    newPoly[j] ^= poly[j];

                    // constant term: poly[j] * a^i  ->  goes into newPoly[j+1]
                    newPoly[j + 1] ^= Gf256.Multiply(poly[j], alphaI);
                }

                poly = newPoly;
            }

            return poly;
        });

    /// <summary>
    /// Computes the Reed-Solomon ECC codewords for the given data using polynomial long division.
    /// </summary>
    /// <param name="data">The data codewords (message polynomial coefficients, highest degree first).</param>
    /// <param name="eccCount">The number of error correction codewords to generate.</param>
    /// <returns>A byte array of length <paramref name="eccCount"/> containing the ECC codewords.</returns>
    internal static byte[] ComputeEcc(ReadOnlySpan<byte> data, int eccCount)
    {
        var generator = GetGeneratorPolynomial(eccCount);
        var work = new byte[data.Length + eccCount];
        data.CopyTo(work);

        for (var i = 0; i < data.Length; i++)
        {
            var coeff = work[i];
            if (coeff == 0)
            {
                continue;
            }

            for (var j = 0; j < generator.Length; j++)
            {
                work[i + j] ^= Gf256.Multiply(generator[j], coeff);
            }
        }

        // The remainder (ECC codewords) is in the last eccCount positions
        var ecc = new byte[eccCount];
        Array.Copy(work, data.Length, ecc, 0, eccCount);
        return ecc;
    }
}
