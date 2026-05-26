// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Rs512SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Rs512;

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        RsaSignatureVerification.Verify(
            publicKeyCbor, data, signature, Algorithm, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
}
