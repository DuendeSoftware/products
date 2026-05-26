// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal static class WebAuthnCrypto
{
    internal static string GenerateChallenge(int size)
    {
        var challengeBytes = new byte[size];
        RandomNumberGenerator.Fill(challengeBytes);
        return Base64Url.EncodeToString(challengeBytes);
    }

    internal static byte[] CombineBytes(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        a.CopyTo(result, 0);
        b.CopyTo(result, a.Length);
        return result;
    }
}
