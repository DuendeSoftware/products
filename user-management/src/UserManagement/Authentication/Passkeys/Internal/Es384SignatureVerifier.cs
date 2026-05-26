// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal sealed class Es384SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => CoseAlgorithms.Es384;

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        EcdsaSignatureVerification.Verify(
            publicKeyCbor,
            data,
            signature,
            expectedAlgorithm: Algorithm,
            expectedCurve: CoseConstants.Curves.P384,
            expectedCoordinateLength: CoseConstants.Curves.P384CoordinateLength,
            ECCurve.NamedCurves.nistP384,
            HashAlgorithmName.SHA384);
}
