// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// GF(2^8) arithmetic using the primitive polynomial 0x11D (x^8 + x^4 + x^3 + x^2 + 1),
/// as specified by the QR code standard (ISO 18004).
/// </summary>
internal static class Gf256
{
    /// <summary>
    /// Exponential (anti-log) table: Exp[i] = α^i in GF(256).
    /// Contains 256 entries; index 255 wraps to Exp[0] for convenience.
    /// </summary>
    internal static readonly byte[] Exp = new byte[256];

    /// <summary>
    /// Logarithm table: Log[v] = i where α^i = v. Log[0] is undefined and set to -1.
    /// </summary>
    internal static readonly int[] Log = new int[256];

    static Gf256()
    {
        var val = 1;
        for (var i = 0; i < 255; i++)
        {
            Exp[i] = (byte)val;
            Log[val] = i;
            val <<= 1;
            if (val >= 256)
            {
                val ^= 0x11D;
            }
        }

        // α^255 = α^0 = 1 (wrap for convenience)
        Exp[255] = Exp[0];

        // Log[0] is undefined
        Log[0] = -1;
    }

    /// <summary>
    /// Multiplies two elements in GF(256) using the exp/log tables.
    /// </summary>
    internal static byte Multiply(byte a, byte b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        return Exp[(Log[a] + Log[b]) % 255];
    }
}
