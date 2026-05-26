// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Ps256SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Ps256;

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        RsaSignatureVerification.Verify(
            publicKeyCbor, data, signature, Algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
}
