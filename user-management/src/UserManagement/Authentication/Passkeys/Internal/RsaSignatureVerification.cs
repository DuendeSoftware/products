// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal static class RsaSignatureVerification
{
    internal static bool Verify(
        byte[] publicKeyCbor,
        byte[] data,
        byte[]? signature,
        int expectedAlgorithm,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding padding)
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
            byte[]? modulus = null;
            byte[]? exponent = null;

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
                    case CoseConstants.Labels.RsaModulus:
                        modulus = reader.ReadByteString();
                        break;
                    case CoseConstants.Labels.RsaExponent:
                        exponent = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            if (keyType != CoseConstants.KeyTypes.Rsa ||
                algorithm != expectedAlgorithm ||
                modulus == null ||
                exponent == null)
            {
                return false;
            }

            using var rsa = RSA.Create();

            var parameters = new RSAParameters
            {
                Modulus = modulus,
                Exponent = exponent
            };

            rsa.ImportParameters(parameters);

            if (rsa.KeySize < 2048)
            {
                return false;
            }

            return rsa.VerifyData(data, signature, hashAlgorithm, padding);
        }
#pragma warning disable CA1031 // Verifier contract returns false for all failures
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }
}
