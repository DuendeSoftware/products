// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Rs1SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Rs1;

#pragma warning disable CA5350 // SHA-1 is retained for legacy TPM attestation compatibility.
    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        RsaSignatureVerification.Verify(
            publicKeyCbor, data, signature, Algorithm, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
#pragma warning restore CA5350
}
