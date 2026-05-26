// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Ps512SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Ps512;

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        RsaSignatureVerification.Verify(
            publicKeyCbor, data, signature, Algorithm, HashAlgorithmName.SHA512, RSASignaturePadding.Pss);
}
