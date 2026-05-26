// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class EcdsaSignatureVerifierTests
{
    private static readonly Dictionary<string, VerifierData> Verifiers = new()
    {
        [nameof(CoseAlgorithms.Es256)] = new VerifierData(
            new Es256SignatureVerifier(),
            HashAlgorithmName.SHA256,
            ECCurve.NamedCurves.nistP256,
            CoseConstants.Curves.P256,
            CoseConstants.Curves.P384),
        [nameof(CoseAlgorithms.Es384)] = new VerifierData(
            new Es384SignatureVerifier(),
            HashAlgorithmName.SHA384,
            ECCurve.NamedCurves.nistP384,
            CoseConstants.Curves.P384,
            CoseConstants.Curves.P256),
        [nameof(CoseAlgorithms.Es512)] = new VerifierData(
            new Es512SignatureVerifier(),
            HashAlgorithmName.SHA512,
            ECCurve.NamedCurves.nistP521,
            CoseConstants.Curves.P521,
            CoseConstants.Curves.P256),
    };

    public static TheoryData<string> VerifierNames => [.. Verifiers.Keys];

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Valid_signature_verifies(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(descriptor.Curve);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = ecdsa.SignData(data, descriptor.HashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
        var coseKey = BuildCoseKey(ecdsa, descriptor.Verifier.Algorithm, descriptor.CoseCurve);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Tampered_signature_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(descriptor.Curve);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = ecdsa.SignData(data, descriptor.HashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
        signature[0] ^= 0xFF;
        var coseKey = BuildCoseKey(ecdsa, descriptor.Verifier.Algorithm, descriptor.CoseCurve);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Wrong_algorithm_rejected(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(descriptor.Curve);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = ecdsa.SignData(data, descriptor.HashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
        var algorithm = descriptor.Verifier.Algorithm == CoseAlgorithms.Es256
            ? CoseAlgorithms.Es384
            : CoseAlgorithms.Es256;
        var coseKey = BuildCoseKey(ecdsa, algorithm, descriptor.CoseCurve);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Wrong_curve_rejected(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(descriptor.Curve);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = ecdsa.SignData(data, descriptor.HashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
        var coseKey = BuildCoseKey(ecdsa, descriptor.Verifier.Algorithm, descriptor.WrongCoseCurve);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Empty_signature_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(descriptor.Curve);
        byte[] data = [1, 2, 3, 4, 5];
        var coseKey = BuildCoseKey(ecdsa, descriptor.Verifier.Algorithm, descriptor.CoseCurve);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, []);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Null_signature_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var ecdsa = ECDsa.Create(descriptor.Curve);
        byte[] data = [1, 2, 3, 4, 5];
        var coseKey = BuildCoseKey(ecdsa, descriptor.Verifier.Algorithm, descriptor.CoseCurve);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, null);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Wrong_key_type_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        using var rsa = RSA.Create(2048);
        byte[] data = [1, 2, 3, 4, 5];
        var signature = rsa.SignData(data, descriptor.HashAlgorithm, RSASignaturePadding.Pkcs1);
        var coseKey = RsaSignatureVerifierTests.BuildCoseKey(rsa, CoseAlgorithms.Rs256);

        var result = descriptor.Verifier.VerifySignature(coseKey, data, signature);

        result.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(VerifierNames))]
    internal void Malformed_cbor_fails(string verifier)
    {
        var descriptor = Verifiers[verifier];
        byte[] data = [1, 2, 3, 4, 5];
        byte[] signature = [1, 2, 3];
        var malformedCbor = new byte[] { 0xFF };

        var result = descriptor.Verifier.VerifySignature(malformedCbor, data, signature);

        result.ShouldBeFalse();
    }

    internal static byte[] BuildCoseKey(ECDsa ecdsa, int algorithm, int curve)
    {
        var parameters = ecdsa.ExportParameters(false);

        var writer = new CborWriter();
        writer.WriteStartMap(5);

        writer.WriteInt32(CoseConstants.Labels.KeyType);
        writer.WriteInt32(CoseConstants.KeyTypes.Ec2);

        writer.WriteInt32(CoseConstants.Labels.Algorithm);
        writer.WriteInt32(algorithm);

        writer.WriteInt32(CoseConstants.Labels.EcCurve);
        writer.WriteInt32(curve);

        writer.WriteInt32(CoseConstants.Labels.EcX);
        writer.WriteByteString(parameters.Q.X!);

        writer.WriteInt32(CoseConstants.Labels.EcY);
        writer.WriteByteString(parameters.Q.Y!);

        writer.WriteEndMap();

        return writer.Encode();
    }

    private sealed record VerifierData(
        ISignatureVerifier Verifier, HashAlgorithmName HashAlgorithm, ECCurve Curve, int CoseCurve, int WrongCoseCurve);
}
