// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public static class PackedAttestationFormatValidatorTests
{
    private const int UnsupportedAlgorithm = -999;
    private const int UnknownKeyType = 0;
    private const int MinimumAuthDataLength = 37; // rpIdHash(32) + flags(1) + signCounter(4)
    private const int AaguidOffsetInAuthData = MinimumAuthDataLength; // AAGUID starts right after the minimum auth data
    private const int AuthDataWithAttestedCredentialLength = 53; // MinAuthDataLength + aaguid(16)
    private const int Sha256HashLength = 32;

    [Fact]
    public static async Task Validate_with_missing_alg_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new[] { new TestSignatureVerifier(CoseAlgorithms.Es256, true) }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["sig"] = new byte[] { 1, 2, 3 }
            },
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(UnknownKeyType, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "alg");
    }

    [Fact]
    public static async Task Validate_with_wrong_algorithm_type_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new[] { new TestSignatureVerifier(CoseAlgorithms.Es256, true) }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = "wrong-algorithm-specifier",
                ["sig"] = new byte[] { 1, 2, 3 }
            },
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(UnknownKeyType, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AttestationValidationResult.Failure>();
    }

    [Fact]
    public static async Task Validate_with_missing_sig_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new[] { new TestSignatureVerifier(CoseAlgorithms.Es256, true) }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Es256
            },
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(UnknownKeyType, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "sig");
    }

    [Fact]
    public static async Task Validate_with_ecdaa_key_id_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new[] { new TestSignatureVerifier(CoseAlgorithms.Es256, true) }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Es256,
                ["sig"] = new byte[] { 1, 2, 3 },
                ["ecdaaKeyId"] = new byte[] { 4, 5, 6 }
            },
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(UnknownKeyType, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "ECDAA");
    }

    [Fact]
    public static async Task Validate_self_attestation_with_alg_mismatch_should_fail()
    {
        var (key, coseKey) = MakeEs256CoseKey();
        using var _ = key;
        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[]
            { new Es256SignatureVerifier(), new Rs256SignatureVerifier() }, TimeProvider.System);
        var context = MakeRealSelfAttestationContext(key, coseKey, attStmtAlgorithm: CoseAlgorithms.Rs256);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.AlgorithmMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Algorithm in attestation statement does not match credential public key algorithm.");
    }

    [Fact]
    public static async Task Validate_self_attestation_with_unsupported_algorithm_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new[] { new TestSignatureVerifier(CoseAlgorithms.Es256, true) }, TimeProvider.System);
        var context =
            MakeSelfAttestationContext(UnsupportedAlgorithm,
                new CoseKey(CoseConstants.KeyTypes.Ec2, UnsupportedAlgorithm, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.UnsupportedAlgorithm);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, $"{UnsupportedAlgorithm}");
    }

    [Fact]
    public static async Task Validate_self_attestation_with_tampered_signature_should_fail()
    {
        var (key, coseKey) = MakeEs256CoseKey();
        using var _ = key;
        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeRealSelfAttestationContext(key, coseKey);
        TamperWithSignature((byte[])context.AttStmt["sig"]!);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.SignatureVerificationFailed);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "signature");
    }

    [Fact]
    public static async Task Validate_self_attestation_with_valid_signature_should_succeed()
    {
        var (key, coseKey) = MakeEs256CoseKey();
        using var ecdsaKey = key;
        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeRealSelfAttestationContext(key, coseKey);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    [Fact]
    public static async Task Validate_full_attestation_with_empty_X5c_array_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Es256,
                ["sig"] = new byte[] { 1, 2, 3 },
                ["x5c"] = Array.Empty<object?>()
            },
            AuthData: new byte[AuthDataWithAttestedCredentialLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(CoseConstants.KeyTypes.Ec2, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "x5c");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_null_first_cert_in_X5c_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Es256,
                ["sig"] = new byte[] { 1, 2, 3 },
                ["x5c"] = new object?[] { null }
            },
            AuthData: new byte[AuthDataWithAttestedCredentialLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(CoseConstants.KeyTypes.Ec2, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "certificate");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_invalid_cert_should_fail()
    {
        var validator = new PackedAttestationFormatValidator(new[] { new TestSignatureVerifier(CoseAlgorithms.Es256, true) }, TimeProvider.System);
        var invalidCertificate = new byte[] { 1, 2, 3 };
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Es256,
                ["sig"] = new byte[] { 1, 2, 3 },
                ["x5c"] = new object?[] { invalidCertificate }
            },
            AuthData: new byte[AuthDataWithAttestedCredentialLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(CoseConstants.KeyTypes.Ec2, CoseAlgorithms.Es256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "certificate");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_unsupported_algorithm_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey);

        var certBytes = cert.Export(X509ContentType.Cert);
        var authData = new byte[AuthDataWithAttestedCredentialLength];
        var clientDataHash = new byte[Sha256HashLength];

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)UnsupportedAlgorithm,
                ["sig"] = new byte[] { 1, 2, 3 },
                ["x5c"] = new object?[] { certBytes }
            },
            AuthData: authData,
            ClientDataHash: clientDataHash,
            CredentialPublicKey: new CoseKey(CoseConstants.KeyTypes.Ec2, UnsupportedAlgorithm, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.UnsupportedAlgorithm);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, $"{UnsupportedAlgorithm}");
    }

    [Fact]
    public static async Task Validate_full_attestation_Es256_with_tampered_signature_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);
        TamperWithSignature((byte[])context.AttStmt["sig"]!);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.SignatureVerificationFailed);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "signature");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_bad_subject_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey,
            subject: "CN=Test, OU=Not Authenticator Attestation, O=Test Org, C=US");

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "subject");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_missing_o_in_subject_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        // Subject has OU but no distinct O component — old string-based check falsely passed
        // because "OU=..." contains the substring "O=".
        using var cert = MakeFullAttestationCertificate(ecKey, subject: "CN=Test, OU=Authenticator Attestation, C=US");

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "subject");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_ou_superstring_value_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        // OU value is a superstring of the required "Authenticator Attestation" —
        // old string-based Contains() check falsely passed.
        using var cert = MakeFullAttestationCertificate(ecKey,
            subject: "CN=Test, OU=Authenticator Attestation Bypass, O=Test Org, C=US");

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "subject");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_missing_ou_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        // No OU component at all — boundary test for the new OID-based implementation.
        using var cert = MakeFullAttestationCertificate(ecKey, subject: "CN=Test, O=Test Org, C=US");

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "subject");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_CA_cert_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey, isCertificateAuthority: true);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "CA");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_expired_cert_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey,
            notBefore: DateTimeOffset.UtcNow.AddDays(-365),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "validity");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_not_yet_valid_cert_should_fail()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey,
            notBefore: DateTimeOffset.UtcNow.AddDays(1),
            notAfter: DateTimeOffset.UtcNow.AddDays(365));

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "validity");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_invalid_Aaguid_extension_format_should_fail()
    {
        // Wrong tag (0x05 instead of 0x04) — invalid DER format
        var invalidAaguidExtRaw = new byte[] { 0x05, 0x10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey, aaguidExtensionRawData: invalidAaguidExtRaw);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.AaguidMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "invalid format");
    }

    [Fact]
    public static async Task Validate_full_attestation_with_mismatched_Aaguid_extension_should_fail()
    {
        // Cert AAGUID: all 0x01; authData AAGUID: all 0x02
        var certAaguid = new byte[16];
        Array.Fill(certAaguid, (byte)0x01);
        var authDataAaguid = new byte[16];
        Array.Fill(authDataAaguid, (byte)0x02);

        var aaguidExtRaw = new byte[18];
        aaguidExtRaw[0] = 0x04;
        aaguidExtRaw[1] = 0x10;
        certAaguid.CopyTo(aaguidExtRaw, 2);

        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey, aaguidExtensionRawData: aaguidExtRaw);

        var authData = new byte[AuthDataWithAttestedCredentialLength];
        authDataAaguid.CopyTo(authData, AaguidOffsetInAuthData);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert, authData: authData);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.AaguidMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "AAGUID");
    }

    [Fact]
    public static async Task Validate_full_attestation_Es256_with_valid_signature_should_succeed()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    [Fact]
    public static async Task Validate_full_attestation_Rs256_with_valid_signature_should_succeed()
    {
        using var rsaKey = RSA.Create(2048);
        var certReq = new CertificateRequest(
            "CN=Test, OU=Authenticator Attestation, O=Test Org, C=US",
            rsaKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        certReq.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        using var cert = certReq.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        var certBytes = cert.Export(X509ContentType.Cert);
        var authData = new byte[AuthDataWithAttestedCredentialLength];
        var clientDataHash = new byte[Sha256HashLength];
        var signedData = WebAuthnCrypto.CombineBytes(authData, clientDataHash);
        var sig = rsaKey.SignData(signedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Rs256SignatureVerifier() }, TimeProvider.System);
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Rs256,
                ["sig"] = sig,
                ["x5c"] = new object?[] { certBytes }
            },
            AuthData: authData,
            ClientDataHash: clientDataHash,
            CredentialPublicKey: new CoseKey(CoseConstants.KeyTypes.Rsa, CoseAlgorithms.Rs256, []));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    [Fact]
    public static async Task Validate_full_attestation_with_matching_Aaguid_extension_should_succeed()
    {
        var aaguid = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        // tag(0x04) + length(0x10) + 16-byte AAGUID
        var aaguidExtRaw = new byte[] { 0x04, 0x10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = MakeFullAttestationCertificate(ecKey, aaguidExtensionRawData: aaguidExtRaw);

        var authData = new byte[AuthDataWithAttestedCredentialLength];
        aaguid.CopyTo(authData, AaguidOffsetInAuthData);

        var validator = new PackedAttestationFormatValidator(new ISignatureVerifier[] { new Es256SignatureVerifier() }, TimeProvider.System);
        var context = MakeFullAttestationEs256Context(ecKey, cert, authData: authData);

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    private sealed class TestSignatureVerifier(int algorithm, bool result) : ISignatureVerifier
    {
        public int Algorithm => algorithm;
        public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) => result;
    }

    private static (ECDsa Key, CoseKey CoseKey) MakeEs256CoseKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(false);

        var writer = new CborWriter();
        writer.WriteStartMap(5);

        writer.WriteInt32(CoseConstants.Labels.KeyType);
        writer.WriteInt32(CoseConstants.KeyTypes.Ec2);

        writer.WriteInt32(CoseConstants.Labels.Algorithm);
        writer.WriteInt32(CoseAlgorithms.Es256);

        writer.WriteInt32(CoseConstants.Labels.EcCurve);
        writer.WriteInt32(CoseConstants.Curves.P256);

        writer.WriteInt32(CoseConstants.Labels.EcX);
        writer.WriteByteString(parameters.Q.X!);

        writer.WriteInt32(CoseConstants.Labels.EcY);
        writer.WriteByteString(parameters.Q.Y!);

        writer.WriteEndMap();
        var rawCbor = writer.Encode();

        var coseKey = new CoseKey(CoseConstants.KeyTypes.Ec2, CoseAlgorithms.Es256, rawCbor);
        return (key, coseKey);
    }

    private static AttestationContext MakeRealSelfAttestationContext(
        ECDsa key,
        CoseKey coseKey,
        int algorithm = CoseAlgorithms.Es256,
        int? attStmtAlgorithm = null)
    {
        var authData = new byte[MinimumAuthDataLength];
        var clientDataHash = new byte[Sha256HashLength];
        var signedData = WebAuthnCrypto.CombineBytes(authData, clientDataHash);
        var sig = key.SignData(signedData, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)(attStmtAlgorithm ?? algorithm),
                ["sig"] = sig
            },
            AuthData: authData,
            ClientDataHash: clientDataHash,
            CredentialPublicKey: coseKey);
    }

    private static X509Certificate2 MakeFullAttestationCertificate(
        ECDsa key,
        string subject = "CN=Test, OU=Authenticator Attestation, O=Test Org, C=US",
        bool isCertificateAuthority = false,
        byte[]? aaguidExtensionRawData = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        var certReq = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        certReq.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(isCertificateAuthority, false, 0, isCertificateAuthority));

        if (aaguidExtensionRawData is not null)
        {
            certReq.CertificateExtensions.Add(
                new X509Extension("1.3.6.1.4.1.45724.1.1.4", aaguidExtensionRawData, critical: false));
        }

        return certReq.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(365));
    }

    private static AttestationContext MakeFullAttestationEs256Context(
        ECDsa key,
        X509Certificate2 cert,
        byte[]? authData = null)
    {
        var certBytes = cert.Export(X509ContentType.Cert);
        authData ??= new byte[AuthDataWithAttestedCredentialLength];
        var clientDataHash = new byte[Sha256HashLength];
        var signedData = WebAuthnCrypto.CombineBytes(authData, clientDataHash);
        var sig = key.SignData(signedData, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)CoseAlgorithms.Es256,
                ["sig"] = sig,
                ["x5c"] = new object?[] { certBytes }
            },
            AuthData: authData,
            ClientDataHash: clientDataHash,
            CredentialPublicKey: new CoseKey(CoseConstants.KeyTypes.Ec2, CoseAlgorithms.Es256, []));
    }

    private static AttestationContext MakeSelfAttestationContext(
        int algorithm,
        CoseKey credentialPublicKey,
        byte[]? sig = null) =>
        new(
            AttStmt: new Dictionary<string, object?>
            {
                ["alg"] = (long)algorithm,
                ["sig"] = sig ?? new byte[] { 1, 2, 3 }
            },
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: credentialPublicKey);

    private static void TamperWithSignature(byte[] signature) => signature[0] = (byte)~signature[0];
}
