// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Es256SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Es256;

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        EcdsaSignatureVerification.Verify(
            publicKeyCbor,
            data,
            signature,
            expectedAlgorithm: Algorithm,
            expectedCurve: CoseConstants.Curves.P256,
            expectedCoordinateLength: CoseConstants.Curves.P256CoordinateLength,
            ECCurve.NamedCurves.nistP256,
            HashAlgorithmName.SHA256);
}
