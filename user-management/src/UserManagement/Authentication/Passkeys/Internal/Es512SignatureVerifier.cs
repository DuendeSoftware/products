// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Es512SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Es512;

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        EcdsaSignatureVerification.Verify(
            publicKeyCbor,
            data,
            signature,
            expectedAlgorithm: Algorithm,
            expectedCurve: CoseConstants.Curves.P521,
            expectedCoordinateLength: CoseConstants.Curves.P521CoordinateLength,
            ECCurve.NamedCurves.nistP521,
            HashAlgorithmName.SHA512);
}
