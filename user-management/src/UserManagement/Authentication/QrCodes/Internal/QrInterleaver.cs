// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// Splits data codewords into blocks, computes Reed-Solomon error correction
/// for each block, and interleaves the results column-wise as required by
/// ISO/IEC 18004 Section 8.6.
/// </summary>
internal static class QrInterleaver
{
    /// <summary>
    /// Interleaves data and ECC codewords for the given QR version and error correction level.
    /// </summary>
    /// <param name="dataCodewords">
    /// The encoded data codewords (length must equal the version's <see cref="QrVersionInfo.DataCodewords"/>).
    /// </param>
    /// <param name="version">The QR code version (1-40).</param>
    /// <param name="ecc">The error correction level.</param>
    /// <returns>
    /// A byte array of length equal to <see cref="QrVersionInfo.TotalCodewords"/>
    /// containing the interleaved data and ECC codewords.
    /// </returns>
    internal static byte[] Interleave(byte[] dataCodewords, int version, QrEccLevel ecc)
    {
        var info = QrVersionTables.Get(version, ecc);

        // Step 1: Split data into blocks according to block groups
        var blocks = new List<byte[]>();
        var offset = 0;

        foreach (var group in info.BlockGroups)
        {
            for (var b = 0; b < group.Count; b++)
            {
                var block = new byte[group.DataCodewords];
                Array.Copy(dataCodewords, offset, block, 0, group.DataCodewords);
                offset += group.DataCodewords;
                blocks.Add(block);
            }
        }

        // Step 2: Compute RS ECC for each block
        var eccBlocks = new List<byte[]>(blocks.Count);
        foreach (var block in blocks)
        {
            eccBlocks.Add(ReedSolomon.ComputeEcc(block, info.EccCodewordsPerBlock));
        }

        // Step 3: Interleave data blocks column-wise
        var maxDataLength = 0;
        foreach (var block in blocks)
        {
            if (block.Length > maxDataLength)
            {
                maxDataLength = block.Length;
            }
        }

        var result = new List<byte>(info.TotalCodewords);

        for (var i = 0; i < maxDataLength; i++)
        {
            foreach (var block in blocks)
            {
                if (i < block.Length)
                {
                    result.Add(block[i]);
                }
            }
        }

        // Step 4: Interleave ECC blocks column-wise
        // All ECC blocks have the same length (EccCodewordsPerBlock), but use
        // the same pattern for consistency.
        var maxEccLength = info.EccCodewordsPerBlock;

        for (var i = 0; i < maxEccLength; i++)
        {
            foreach (var eccBlock in eccBlocks)
            {
                if (i < eccBlock.Length)
                {
                    result.Add(eccBlock[i]);
                }
            }
        }

        return result.ToArray();
    }
}
