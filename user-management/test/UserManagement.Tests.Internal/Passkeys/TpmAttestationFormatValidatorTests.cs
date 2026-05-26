// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class TpmAttestationFormatValidatorTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private const string TpmManufacturerOid = "2.23.133.2.1";
    private const string TpmModelOid = "2.23.133.2.2";
    private const string TpmVersionOid = "2.23.133.2.3";
    private const int UnknownKeyType = 0;
    private const int MinimumAuthDataLength = 37; // rpIdHash(32) + flags(1) + signCounter(4)
    private const int AaguidOffsetInAuthData = MinimumAuthDataLength;
    private const int AuthDataWithAttestedCredentialLength = 53; // header + AAGUID
    private const int Sha256HashLength = 32;

    // Version gate. TPM attestation must declare version "2.0".

    [Fact]
    public async Task Rejects_attestation_without_version_declaration()
    {
        var validator = CreateValidator();
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>(),
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(UnknownKeyType, CoseAlgorithms.Rs256, []));

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "missing required 'ver' field");
    }

    [Fact]
    public async Task Rejects_attestation_with_unsupported_version()
    {
        var validator = CreateValidator();
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["ver"] = "1.2"
            },
            AuthData: new byte[MinimumAuthDataLength],
            ClientDataHash: new byte[Sha256HashLength],
            CredentialPublicKey: new CoseKey(UnknownKeyType, CoseAlgorithms.Rs256, []));

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "value is not '2.0'");
    }

    // Parsing the attestation structure. certInfo must be a valid TPMS_ATTEST.
    // The magic value proves TPM origin; the type proves it is a key certification.

    [Fact]
    public async Task Rejects_attestation_without_cert_info()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmtWithout(fixture.Context.AttStmt, "certInfo");
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Attestation statement is missing required 'certInfo' field.");
    }

    [Fact]
    public async Task Rejects_cert_info_with_invalid_magic_proving_non_tpm_origin()
    {
        uint bogusMagic = 0x12345678;
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        attStmt["certInfo"] = CreateTpmsAttestBytes(
            bogusMagic,
            TpmConstants.StAttestCertify,
            [],
            fixture.ExpectedExtraData,
            0,
            0,
            0,
            0,
            0,
            fixture.AttestedName,
            []);
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Invalid TPMS_ATTEST magic value");
    }

    [Fact]
    public async Task Rejects_cert_info_with_wrong_type_proving_it_is_not_a_key_certification()
    {
        ushort invalidType = 0x9999;
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        attStmt["certInfo"] = CreateTpmsAttestBytes(
            TpmConstants.GeneratedValue,
            invalidType,
            [],
            fixture.ExpectedExtraData,
            0,
            0,
            0,
            0,
            0,
            fixture.AttestedName,
            []);
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Invalid TPMS_ATTEST type value");
    }

    [Fact]
    public async Task Rejects_attestation_without_pub_area()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmtWithout(fixture.Context.AttStmt, "pubArea");
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Attestation statement is missing required 'pubArea' field.");
    }

    // Challenge binding. extraData must equal hash(authData || clientDataHash).
    // This proves the TPM saw the same registration ceremony the relying party initiated.

    [Fact]
    public async Task Rejects_extra_data_mismatch_proving_challenge_binding_is_broken()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var badExtraData = fixture.ExpectedExtraData.ToArray();
        badExtraData[0] = (byte)~badExtraData[0];
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        attStmt["certInfo"] = CreateTpmsAttestBytes(
            TpmConstants.GeneratedValue,
            TpmConstants.StAttestCertify,
            [],
            badExtraData,
            0,
            0,
            0,
            0,
            0,
            fixture.AttestedName,
            []);
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "'extraData' does not match the hash of authData || clientDataHash");
    }

    // Key binding. attested.name must match hash(pubArea), proving the TPM is
    // certifying this specific key. Then pubArea.unique must match the credential public key,
    // confirming the TPM-generated key is the one the authenticator reported.

    [Fact]
    public async Task Rejects_attested_name_mismatch_proving_tpm_certified_a_different_key()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var wrongName = fixture.AttestedName.ToArray();
        wrongName[^1] = (byte)~wrongName[^1];
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        attStmt["certInfo"] = CreateTpmsAttestBytes(
            TpmConstants.GeneratedValue,
            TpmConstants.StAttestCertify,
            [],
            fixture.ExpectedExtraData,
            0,
            0,
            0,
            0,
            0,
            wrongName,
            []);
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "'attested.name' does not match the supplied 'pubArea'");
    }

    [Fact]
    public async Task Rejects_credential_public_key_not_matching_tpm_pub_area()
    {
        using var fixture = new ValidTpmAttestation();
        using var otherCredentialKey = RSA.Create(2048);
        var validator = CreateValidator();
        var otherCredentialPublicKey = CreateRsaCoseKey(otherCredentialKey.ExportParameters(false));
        var context = CreateContext(fixture.Context.AttStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            otherCredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "'pubArea.unique' does not match the credential public key");
    }

    // Key capability. The TPM key must have the sign/encrypt object attribute,
    // confirming it was created for signing operations.

    [Fact]
    public async Task Rejects_key_without_sign_encrypt_capability()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        var pubAreaBytes = CreateRsaTpmtPublicBytes(
            fixture.CredentialKey.ExportParameters(false),
            objectAttributes: 0);
        var pubArea = TpmtPublic.Parse(pubAreaBytes);
        attStmt["pubArea"] = pubAreaBytes;
        attStmt["certInfo"] = CreateTpmsAttestBytes(
            TpmConstants.GeneratedValue,
            TpmConstants.StAttestCertify,
            [],
            fixture.ExpectedExtraData,
            0,
            0,
            0,
            0,
            0,
            pubArea.ComputeName(pubAreaBytes),
            []);
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "must have the sign/encrypt attribute set");
    }

    // Signature verification. The AIK must have actually signed certInfo.
    // This is the final cryptographic proof that a genuine TPM vouches for this key.
    // Per WebAuthn §8.3 step 7, sig is treated as opaque bytes verified using the
    // algorithm specified in alg.

    [Fact]
    public async Task Rejects_attestation_without_signature()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmtWithout(fixture.Context.AttStmt, "sig");
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "missing required 'sig' field");
    }

    [Fact]
    public async Task Rejects_tampered_signature_over_cert_info()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        var signature = fixture.RawSignature.ToArray();
        signature[0] = (byte)~signature[0];
        attStmt["sig"] = signature;
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.SignatureVerificationFailed);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "TPM attestation signature verification failed");
    }

    // AIK certificate validation. The Attestation Identity Key certificate must
    // meet TPM-specific requirements: empty subject, SAN containing manufacturer/model/version,
    // the tcg-kp-AIKCertificate EKU, not a CA, and valid time period.

    [Fact]
    public async Task Rejects_attestation_without_x5c_certificate_chain()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmtWithout(fixture.Context.AttStmt, "x5c");
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "'x5c' field is invalid");
    }

    [Fact]
    public async Task Rejects_aik_certificate_with_non_empty_subject()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateWithNonEmptySubject(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "subject field must be empty");
    }

    [Fact]
    public async Task Rejects_aik_certificate_missing_required_san_tpm_fields()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateWithMissingSanVersion(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "SAN must contain manufacturer, model, and version");
    }

    [Fact]
    public async Task Rejects_aik_certificate_with_empty_san_attribute_value()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateWithEmptySanManufacturer(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "SAN must contain manufacturer, model, and version");
    }

    [Fact]
    public async Task Accepts_aik_certificate_with_printable_string_san_encoding()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateWithPrintableStringSan(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    [Fact]
    public async Task Rejects_aik_certificate_without_tcg_kp_aik_certificate_eku()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateWithoutEku(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "EKU must contain");
    }

    [Fact]
    public async Task Rejects_aik_certificate_that_is_a_certificate_authority()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateAuthorityCertificate(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "must not be a CA certificate");
    }

    [Fact]
    public async Task Rejects_aik_certificate_without_basic_constraints()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateCertificateWithoutBasicConstraints(fixture.AikKey, fixture.Aaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "must include Basic Constraints with CA=false");
    }

    [Fact]
    public async Task Rejects_aik_certificate_outside_validity_period()
    {
        using var fixture = new ValidTpmAttestation();
        using var certificate = CreateValidCertificate(
            fixture.AikKey,
            fixture.Aaguid,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "not within its validity period");
    }

    // AAGUID consistency. The AAGUID in the AIK certificate extension must match
    // the AAGUID in authData, confirming the certificate belongs to this authenticator.

    [Fact]
    public async Task Rejects_aaguid_mismatch_between_certificate_and_auth_data()
    {
        using var fixture = new ValidTpmAttestation();
        var differentAaguid = fixture.Aaguid.ToArray();
        differentAaguid[0] = (byte)~differentAaguid[0];
        using var certificate = CreateValidCertificate(fixture.AikKey, differentAaguid);
        var validator = CreateValidator();
        var context = CreateContextWithCertificate(fixture, certificate);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.AaguidMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "AAGUID in attestation certificate does not match authenticator data");
    }

    // Certificate chain validation. All x5c entries must be valid, loadable certificates
    // within their validity period.

    [Fact]
    public async Task Rejects_x5c_chain_certificate_that_is_not_valid_der()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        var aikCertBytes = ((object?[])attStmt["x5c"]!)[0];
        attStmt["x5c"] = new object?[] { aikCertBytes, new byte[] { 0x00, 0x01, 0x02 } };
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "not a valid certificate");
    }

    [Fact]
    public async Task Rejects_x5c_chain_certificate_outside_validity_period()
    {
        using var fixture = new ValidTpmAttestation();
        using var expiredChainCert = CreateValidCertificate(
            fixture.AikKey,
            fixture.Aaguid,
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        var aikCertBytes = ((object?[])attStmt["x5c"]!)[0];
        attStmt["x5c"] = new object?[] { aikCertBytes, expiredChainCert.Export(X509ContentType.Cert) };
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "not within its validity period");
    }

    [Fact]
    public async Task Rejects_x5c_chain_entry_that_is_not_a_byte_array()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        var aikCertBytes = ((object?[])attStmt["x5c"]!)[0];
        attStmt["x5c"] = new object?[] { aikCertBytes, "not a byte array" };
        var context = CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);

        var result = await validator.ValidateAsync(context, _ct);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidCertificate);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "is not a byte array");
    }

    [Fact]
    public async Task Accepts_valid_rsa_tpm_attestation()
    {
        using var fixture = new ValidTpmAttestation();
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(fixture.Context, _ct);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    [Fact]
    public async Task Accepts_valid_ecc_tpm_attestation()
    {
        using var fixture = new ValidEcTpmAttestation();
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(fixture.Context, _ct);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    private static TpmAttestationFormatValidator CreateValidator() =>
        new([new Rs256SignatureVerifier(), new Es256SignatureVerifier()],
            new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)));

    private static AttestationContext CreateContext(
        IReadOnlyDictionary<string, object?> attStmt,
        IReadOnlyCollection<byte> authData,
        IReadOnlyCollection<byte> clientDataHash,
        CoseKey credentialPublicKey) =>
        new(attStmt, authData, clientDataHash, credentialPublicKey);

    private static AttestationContext CreateContextWithCertificate(
        ValidTpmAttestation fixture,
        X509Certificate2 certificate)
    {
        var attStmt = CloneAttStmt(fixture.Context.AttStmt);
        attStmt["x5c"] = new object?[] { certificate.Export(X509ContentType.Cert) };
        return CreateContext(attStmt, fixture.Context.AuthData, fixture.Context.ClientDataHash,
            fixture.Context.CredentialPublicKey);
    }

    private static Dictionary<string, object?> CloneAttStmt(IReadOnlyDictionary<string, object?> attStmt) =>
        attStmt.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static Dictionary<string, object?> CloneAttStmtWithout(IReadOnlyDictionary<string, object?> attStmt,
        string key)
    {
        var copy = CloneAttStmt(attStmt);
        _ = copy.Remove(key);
        return copy;
    }

    private static X509Certificate2 CreateValidCertificate(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, true),
            true,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateValidCertificate(ECDsa key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, true),
            true,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateValidCertificate(
        RSA key,
        byte[] aaguid,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, true),
            true,
            false,
            true,
            aaguid,
            notBefore,
            notAfter);

    private static X509Certificate2 CreateCertificateWithNonEmptySubject(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            new X500DistinguishedName("CN=AIK"),
            CreateSubjectAlternativeNameRawData(true, true, true),
            true,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateCertificateWithMissingSanVersion(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, false),
            true,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateCertificateWithEmptySanManufacturer(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(
                (TpmManufacturerOid, string.Empty, UniversalTagNumber.UTF8String),
                (TpmModelOid, "Model", UniversalTagNumber.UTF8String),
                (TpmVersionOid, "1.0", UniversalTagNumber.UTF8String)),
            true,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateCertificateWithPrintableStringSan(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(
                (TpmManufacturerOid, "id:4D534654", UniversalTagNumber.PrintableString),
                (TpmModelOid, "Model", UniversalTagNumber.PrintableString),
                (TpmVersionOid, "1.0", UniversalTagNumber.PrintableString)),
            true,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateCertificateWithoutEku(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, true),
            false,
            false,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateCertificateAuthorityCertificate(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, true),
            true,
            true,
            true,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static X509Certificate2 CreateCertificateWithoutBasicConstraints(RSA key, byte[] aaguid) =>
        CreateCertificate(
            key,
            CreateEmptySubjectName(),
            CreateSubjectAlternativeNameRawData(true, true, true),
            true,
            false,
            false,
            aaguid,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));



    private static X509Certificate2 CreateCertificate(
        RSA key,
        X500DistinguishedName subjectName,
        byte[] sanRawData,
        bool includeEku,
        bool certificateAuthority,
        bool includeBasicConstraints,
        byte[] aaguid,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        var request = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        AddCertificateExtensions(request, sanRawData, includeEku, certificateAuthority, includeBasicConstraints,
            aaguid);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateCertificate(
        ECDsa key,
        X500DistinguishedName subjectName,
        byte[] sanRawData,
        bool includeEku,
        bool certificateAuthority,
        bool includeBasicConstraints,
        byte[] aaguid,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        var request = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256);
        AddCertificateExtensions(request, sanRawData, includeEku, certificateAuthority, includeBasicConstraints,
            aaguid);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static void AddCertificateExtensions(
        CertificateRequest request,
        byte[] sanRawData,
        bool includeEku,
        bool certificateAuthority,
        bool includeBasicConstraints,
        byte[] aaguid)
    {
        request.CertificateExtensions.Add(new X509Extension("2.5.29.17", sanRawData, critical: false));

        if (includeEku)
        {
            var keyUsages = new OidCollection
            {
                new(WebAuthnConstants.Oids.TcgKpAikCertificate)
            };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(keyUsages, critical: false));
        }

        if (includeBasicConstraints)
        {
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority, false, 0, certificateAuthority));
        }

        request.CertificateExtensions.Add(
            new X509Extension(WebAuthnConstants.Oids.FidoAaguidExtension, CreateAaguidExtensionRawData(aaguid),
                critical: false));
    }

    private static X500DistinguishedName CreateEmptySubjectName() => new([0x30, 0x00]);

    private static byte[] CreateSubjectAlternativeNameRawData(bool includeManufacturer, bool includeModel,
        bool includeVersion)
    {
        var attributes = new List<(string Oid, string Value, UniversalTagNumber Tag)>();

        if (includeManufacturer)
        {
            attributes.Add((TpmManufacturerOid, "id:4D534654", UniversalTagNumber.UTF8String));
        }

        if (includeModel)
        {
            attributes.Add((TpmModelOid, "Model", UniversalTagNumber.UTF8String));
        }

        if (includeVersion)
        {
            attributes.Add((TpmVersionOid, "1.0", UniversalTagNumber.UTF8String));
        }

        return CreateSubjectAlternativeNameRawData([.. attributes]);
    }

    private static byte[] CreateSubjectAlternativeNameRawData(
        params (string Oid, string Value, UniversalTagNumber Tag)[] attributes)
    {
        var directoryName = CreateDirectoryNameRawData(attributes);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        var directoryNameTag = new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true);

        using (writer.PushSequence())
        {
            using (writer.PushSequence(directoryNameTag))
            {
                writer.WriteEncodedValue(directoryName);
            }
        }

        return writer.Encode();
    }

    private static byte[] CreateDirectoryNameRawData(
        params (string Oid, string Value, UniversalTagNumber Tag)[] attributes)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            foreach (var attribute in attributes)
            {
                WriteRelativeDistinguishedName(writer, attribute.Oid, attribute.Value, attribute.Tag);
            }
        }

        return writer.Encode();
    }

    private static void WriteRelativeDistinguishedName(
        AsnWriter writer,
        string oid,
        string value,
        UniversalTagNumber stringTag)
    {
        using (writer.PushSetOf())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(oid);
                writer.WriteCharacterString(stringTag, value);
            }
        }
    }

    private static byte[] CreateAaguidExtensionRawData(byte[] aaguid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteOctetString(aaguid);
        return writer.Encode();
    }

    private static CoseKey CreateRsaCoseKey(RSAParameters parameters)
    {
        var writer = new CborWriter();
        writer.WriteStartMap(4);

        writer.WriteInt32(CoseConstants.Labels.KeyType);
        writer.WriteInt32(CoseConstants.KeyTypes.Rsa);

        writer.WriteInt32(CoseConstants.Labels.Algorithm);
        writer.WriteInt32(CoseAlgorithms.Rs256);

        writer.WriteInt32(CoseConstants.Labels.RsaModulus);
        writer.WriteByteString(parameters.Modulus!);

        writer.WriteInt32(CoseConstants.Labels.RsaExponent);
        writer.WriteByteString(parameters.Exponent!);

        writer.WriteEndMap();
        var rsaCbor = writer.Encode();
        _ = CoseKey.TryParse(rsaCbor, out var rsaKey);
        return rsaKey!;
    }

    private static CoseKey CreateEcCoseKey(ECParameters parameters)
    {
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
        var ecCbor = writer.Encode();
        _ = CoseKey.TryParse(ecCbor, out var ecKey);
        return ecKey!;
    }

    private static byte[] CreateRsaTpmtPublicBytes(RSAParameters parameters) =>
        CreateRsaTpmtPublicBytes(parameters, TpmConstants.ObjectAttributeSignEncrypt);

    private static byte[] CreateRsaTpmtPublicBytes(RSAParameters parameters, uint objectAttributes)
    {
        var modulus = parameters.Modulus!;
        Span<byte> exponentBuffer = stackalloc byte[sizeof(uint)];
        parameters.Exponent!.CopyTo(exponentBuffer[(exponentBuffer.Length - parameters.Exponent!.Length)..]);
        var exponent = BinaryPrimitives.ReadUInt32BigEndian(exponentBuffer);
        var buffer = new List<byte>();
        AddUInt16(buffer, TpmConstants.AlgRsa);
        AddUInt16(buffer, TpmConstants.AlgSha256);
        AddUInt32(buffer, objectAttributes);
        AddTpm2B(buffer, []);
        AddUInt16(buffer, TpmConstants.AlgNull);
        AddUInt16(buffer, TpmConstants.AlgNull);
        AddUInt16(buffer, (ushort)(modulus.Length * 8));
        AddUInt32(buffer, exponent);
        AddTpm2B(buffer, modulus);
        return [.. buffer];
    }

    private static byte[] CreateEccTpmtPublicBytes(ECParameters parameters) =>
        CreateEccTpmtPublicBytes(parameters, TpmConstants.ObjectAttributeSignEncrypt);

    private static byte[] CreateEccTpmtPublicBytes(ECParameters parameters, uint objectAttributes)
    {
        var buffer = new List<byte>();
        AddUInt16(buffer, TpmConstants.AlgEcc);
        AddUInt16(buffer, TpmConstants.AlgSha256);
        AddUInt32(buffer, objectAttributes);
        AddTpm2B(buffer, []);
        AddUInt16(buffer, TpmConstants.AlgNull);
        AddUInt16(buffer, TpmConstants.AlgNull);
        AddUInt16(buffer, TpmConstants.EccCurveNistP256);
        AddUInt16(buffer, 0);
        AddTpm2B(buffer, parameters.Q.X!);
        AddTpm2B(buffer, parameters.Q.Y!);
        return [.. buffer];
    }

    private static byte[] CreateTpmsAttestBytes(
        uint magic,
        ushort type,
        byte[] qualifiedSigner,
        byte[] extraData,
        ulong clock,
        uint resetCount,
        uint restartCount,
        byte safe,
        ulong firmwareVersion,
        byte[] name,
        byte[] qualifiedName)
    {
        var buffer = new List<byte>();
        AddUInt32(buffer, magic);
        AddUInt16(buffer, type);
        AddTpm2B(buffer, qualifiedSigner);
        AddTpm2B(buffer, extraData);
        AddUInt64(buffer, clock);
        AddUInt32(buffer, resetCount);
        AddUInt32(buffer, restartCount);
        buffer.Add(safe);
        AddUInt64(buffer, firmwareVersion);
        AddTpm2B(buffer, name);
        AddTpm2B(buffer, qualifiedName);
        return [.. buffer];
    }

    private static void AddTpm2B(List<byte> buffer, byte[] value)
    {
        AddUInt16(buffer, (ushort)value.Length);
        buffer.AddRange(value);
    }

    private static void AddUInt16(List<byte> buffer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private static void AddUInt32(List<byte> buffer, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private static void AddUInt64(List<byte> buffer, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private sealed class ValidTpmAttestation : IDisposable
    {
        public ValidTpmAttestation()
        {
            Aaguid = [.. Enumerable.Range(1, 16).Select(value => (byte)value)];
            AuthData = new byte[AuthDataWithAttestedCredentialLength];
            Aaguid.CopyTo(AuthData, AaguidOffsetInAuthData);

            ClientDataHash = [.. Enumerable.Range(0, Sha256HashLength).Select(value => (byte)(value + 1))];

            CredentialKey = RSA.Create(2048);
            var credentialParameters = CredentialKey.ExportParameters(false);
            CredentialPublicKey = CreateRsaCoseKey(credentialParameters);
            PubAreaBytes = CreateRsaTpmtPublicBytes(credentialParameters);
            var pubArea = TpmtPublic.Parse(PubAreaBytes);
            AttestedName = pubArea.ComputeName(PubAreaBytes);
            ExpectedExtraData = SHA256.HashData(WebAuthnCrypto.CombineBytes(AuthData, ClientDataHash));
            CertInfoBytes = CreateTpmsAttestBytes(
                TpmConstants.GeneratedValue,
                TpmConstants.StAttestCertify,
                [],
                ExpectedExtraData,
                0,
                0,
                0,
                0,
                0,
                AttestedName,
                []);

            AikKey = RSA.Create(2048);
            AikCertificate = CreateValidCertificate(AikKey, Aaguid);
            RawSignature = AikKey.SignData(CertInfoBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            Context = new AttestationContext(
                AttStmt: new Dictionary<string, object?>
                {
                    ["ver"] = "2.0",
                    ["alg"] = (long)CoseAlgorithms.Rs256,
                    ["certInfo"] = CertInfoBytes,
                    ["pubArea"] = PubAreaBytes,
                    ["sig"] = RawSignature,
                    ["x5c"] = new object?[] { AikCertificate.Export(X509ContentType.Cert) }
                },
                AuthData: AuthData,
                ClientDataHash: ClientDataHash,
                CredentialPublicKey: CredentialPublicKey);
        }

        public byte[] Aaguid { get; }
        public RSA AikKey { get; }
        private X509Certificate2 AikCertificate { get; }
        public byte[] AttestedName { get; }
        private byte[] AuthData { get; }
        private byte[] CertInfoBytes { get; }
        private byte[] ClientDataHash { get; }
        public AttestationContext Context { get; }
        public RSA CredentialKey { get; }
        private CoseKey CredentialPublicKey { get; }
        public byte[] ExpectedExtraData { get; }
        private byte[] PubAreaBytes { get; }
        public byte[] RawSignature { get; }

        public void Dispose()
        {
            AikCertificate.Dispose();
            AikKey.Dispose();
            CredentialKey.Dispose();
        }
    }

    private sealed class ValidEcTpmAttestation : IDisposable
    {
        public ValidEcTpmAttestation()
        {
            Aaguid = [.. Enumerable.Range(1, 16).Select(value => (byte)(value + 16))];
            AuthData = new byte[AuthDataWithAttestedCredentialLength];
            Aaguid.CopyTo(AuthData, AaguidOffsetInAuthData);

            ClientDataHash = [.. Enumerable.Range(0, Sha256HashLength).Select(value => (byte)(value + 33))];

            CredentialKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var credentialParameters = CredentialKey.ExportParameters(false);
            CredentialPublicKey = CreateEcCoseKey(credentialParameters);
            PubAreaBytes = CreateEccTpmtPublicBytes(credentialParameters);
            var pubArea = TpmtPublic.Parse(PubAreaBytes);
            AttestedName = pubArea.ComputeName(PubAreaBytes);
            ExpectedExtraData = SHA256.HashData(WebAuthnCrypto.CombineBytes(AuthData, ClientDataHash));
            CertInfoBytes = CreateTpmsAttestBytes(
                TpmConstants.GeneratedValue,
                TpmConstants.StAttestCertify,
                [],
                ExpectedExtraData,
                0,
                0,
                0,
                0,
                0,
                AttestedName,
                []);

            AikKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            AikCertificate = CreateValidCertificate(AikKey, Aaguid);
            RawSignature = AikKey.SignData(CertInfoBytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);

            Context = new AttestationContext(
                AttStmt: new Dictionary<string, object?>
                {
                    ["ver"] = "2.0",
                    ["alg"] = (long)CoseAlgorithms.Es256,
                    ["certInfo"] = CertInfoBytes,
                    ["pubArea"] = PubAreaBytes,
                    ["sig"] = RawSignature,
                    ["x5c"] = new object?[] { AikCertificate.Export(X509ContentType.Cert) }
                },
                AuthData: AuthData,
                ClientDataHash: ClientDataHash,
                CredentialPublicKey: CredentialPublicKey);
        }

        private byte[] Aaguid { get; }
        private ECDsa AikKey { get; }
        private X509Certificate2 AikCertificate { get; }
        private byte[] AttestedName { get; }
        private byte[] AuthData { get; }
        private byte[] CertInfoBytes { get; }
        private byte[] ClientDataHash { get; }
        public AttestationContext Context { get; }
        private ECDsa CredentialKey { get; }
        private CoseKey CredentialPublicKey { get; }
        private byte[] ExpectedExtraData { get; }
        private byte[] PubAreaBytes { get; }
        private byte[] RawSignature { get; }

        public void Dispose()
        {
            AikCertificate.Dispose();
            AikKey.Dispose();
            CredentialKey.Dispose();
        }
    }
}
