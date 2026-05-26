// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal static class EcdsaSignatureVerification
{
    internal static bool Verify(
        byte[] publicKeyCbor,
        byte[] data,
        byte[]? signature,
        int expectedAlgorithm,
        int expectedCurve,
        int expectedCoordinateLength,
        ECCurve curve,
        HashAlgorithmName hashAlgorithm)
    {
        try
        {
            if (signature == null || signature.Length == 0)
            {
                return false;
            }

            var reader = new CborReader(publicKeyCbor);

            if (reader.PeekState() != CborReaderState.StartMap)
            {
                return false;
            }

            var mapLength = reader.ReadStartMap();

            int? keyType = null;
            int? algorithm = null;
            int? curveValue = null;
            byte[]? x = null;
            byte[]? y = null;

            for (var i = 0; i < mapLength; i++)
            {
                var label = reader.ReadInt32();

                switch (label)
                {
                    case CoseConstants.Labels.KeyType:
                        keyType = reader.ReadInt32();
                        break;
                    case CoseConstants.Labels.Algorithm:
                        algorithm = reader.ReadInt32();
                        break;
                    case CoseConstants.Labels.EcCurve:
                        curveValue = reader.ReadInt32();
                        break;
                    case CoseConstants.Labels.EcX:
                        x = reader.ReadByteString();
                        break;
                    case CoseConstants.Labels.EcY:
                        y = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            if (keyType != CoseConstants.KeyTypes.Ec2 ||
                algorithm != expectedAlgorithm ||
                curveValue != expectedCurve ||
                x == null ||
                y == null ||
                x.Length != expectedCoordinateLength ||
                y.Length != expectedCoordinateLength)
            {
                return false;
            }

            using var ecdsa = ECDsa.Create(curve);

            var parameters = new ECParameters
            {
                Curve = curve,
                Q = new ECPoint
                {
                    X = x,
                    Y = y
                }
            };

            ecdsa.ImportParameters(parameters);

            return ecdsa.VerifyData(data, signature, hashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
        }
#pragma warning disable CA1031 // Verifier contract returns false for all failures
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }
}
