// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class RsaSignatureVerifierTests
{
    private static readonly Dictionary<string, VerifierData> Verifiers = new()
    {
        [nameof(CoseAlgorithms.Ps256)] = new VerifierData(
            new Ps256SignatureVerifier(),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss),
        [nameof(CoseAlgorithms.Ps384)] = new VerifierData(
            new Ps384SignatureVerifier(),
            HashAlgorithmName.SHA384,
            RSASignaturePadding.Pss),
        [nameof(CoseAlgorithms.Ps512)] = new VerifierData(
            new Ps512SignatureVerifier(),
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pss),
        [nameof(CoseAlgorithms.Rs1)] = new VerifierData(
            new Rs1SignatureVerifier(),
            HashAlgorithmName.SHA1,
            RSASignaturePadding.Pkcs1),
        [nameof(CoseAlgorithms.Rs256)] = new VerifierData(
            new Rs256SignatureVerifier(),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1),
        [nameof(CoseAlgorithms.Rs384)] = new VerifierData(
            new Rs384SignatureVerifier(),
            HashAlgorithmName.SHA384,
            RSASignaturePadding.Pkcs1),
        [nameof(CoseAlgorithms.Rs512)] = new VerifierData(
            new Rs512SignatureVerifier(),
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pkcs1),
    };

    public static TheoryData<string> VerifierNames => [.. Verifiers.Keys];

    public static TheoryData<string, int, bool> MinimumKeySizes
    {
        get
        {
            var data = new TheoryData<string, int, bool>();

            foreach (var name in Verifiers.Keys)
            {
                data.Add(name, 1024, false);
                data.Add(name, 2048, true);
                data.Add(name, 4096, true);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Valid_signature_verifies(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(2048);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = rsa.SignData(data, descriptor.HashAlgorithm, descriptor.Padding);
        var coseKey = BuildCoseKey(rsa, descriptor.Verifier.Algorithm);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Tampered_signature_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(2048);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = rsa.SignData(data, descriptor.HashAlgorithm, descriptor.Padding);
        signature[0] ^= 0xFF;
        var coseKey = BuildCoseKey(rsa, descriptor.Verifier.Algorithm);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Wrong_algorithm_rejected(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(2048);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = rsa.SignData(data, descriptor.HashAlgorithm, descriptor.Padding);
        var algorithm = descriptor.Verifier.Algorithm == CoseAlgorithms.Rs1
            ? CoseAlgorithms.Rs256
            : CoseAlgorithms.Rs1;
        var coseKey = BuildCoseKey(rsa, algorithm);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(MinimumKeySizes))]
    internal void Minimum_key_size_enforced(string verifier, int keySize, bool expectedResult)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(keySize);
        byte[] data = [1, 2, 3, 4, 5];
        byte[]? signature;
        try
        {
            signature = rsa.SignData(data, descriptor.HashAlgorithm, descriptor.Padding);
        }
        catch (CryptographicException)
        {
            signature = [1, 2, 3];
        }

        var coseKey = BuildCoseKey(rsa, descriptor.Verifier.Algorithm);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBe(expectedResult);
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Empty_signature_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(2048);
        byte[] data = [1, 2, 3, 4, 5];
        var coseKey = BuildCoseKey(rsa, descriptor.Verifier.Algorithm);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, []);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Null_signature_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(2048);
        byte[] data = [1, 2, 3, 4, 5];
        var coseKey = BuildCoseKey(rsa, descriptor.Verifier.Algorithm);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, null);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Wrong_key_type_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        var coseKey = EcdsaSignatureVerifierTests.BuildCoseKey(ecdsa, CoseAlgorithms.Es256, CoseConstants.Curves.P256);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Malformed_cbor_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        byte[] malformedCbor = [0xFF];
        byte[] data = [1, 2, 3, 4, 5];
        byte[] signature = [1, 2, 3];

        var result = descriptor.Verifier.VerifySignature(malformedCbor, data, signature);

        result.ShouldBeFalse();
    }

    internal static byte[] BuildCoseKey(RSA rsa, int algorithm)
    {
        var parameters = rsa.ExportParameters(false);

        var writer = new CborWriter();
        writer.WriteStartMap(4);

        writer.WriteInt32(CoseConstants.Labels.KeyType);
        writer.WriteInt32(CoseConstants.KeyTypes.Rsa);

        writer.WriteInt32(CoseConstants.Labels.Algorithm);
        writer.WriteInt32(algorithm);

        writer.WriteInt32(CoseConstants.Labels.RsaModulus);
        writer.WriteByteString(parameters.Modulus!);

        writer.WriteInt32(CoseConstants.Labels.RsaExponent);
        writer.WriteByteString(parameters.Exponent!);

        writer.WriteEndMap();

        return writer.Encode();
    }

    private sealed record VerifierData(
        ISignatureVerifier Verifier, HashAlgorithmName HashAlgorithm, RSASignaturePadding Padding);
}
