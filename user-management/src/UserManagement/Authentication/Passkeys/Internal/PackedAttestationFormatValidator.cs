// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Validates "packed" attestation format per WebAuthn Level 3 §8.2.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PackedAttestationFormatValidator : IAttestationFormatValidator
{
    private const int RequiredX509Version = 3;

    private const int AaguidEnd =
        WebAuthnConstants.AuthenticatorDataLayout.HeaderLength +
        WebAuthnConstants.AuthenticatorDataLayout.AaguidLength;

    private readonly Dictionary<int, ISignatureVerifier> _signatureVerifiers;
    private readonly TimeProvider _timeProvider;

    public PackedAttestationFormatValidator(IEnumerable<ISignatureVerifier> signatureVerifiers, TimeProvider timeProvider)
    {
        _signatureVerifiers = signatureVerifiers.ToDictionary(v => v.Algorithm);
        _timeProvider = timeProvider;
    }

    public string Format => PasskeyConstants.AttestationFormat.Packed;

    public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct)
    {
        // https://www.w3.org/TR/webauthn-3/#sctn-packed-attestation

        // Parse required attStmt fields. CBOR integers are parsed as long.
        if (!context.AttStmt.TryGetValue("alg", out var algObj) || algObj is not long algLong)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement is missing required 'alg' field."));
        }

        if (!context.AttStmt.TryGetValue("sig", out var sigObj) || sigObj is not byte[] sig)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement is missing required 'sig' field."));
        }

        if (algLong is < int.MinValue or > int.MaxValue)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                $"Attestation statement 'alg' value {algLong} is outside the valid COSE algorithm range."));
        }

        var alg = (int)algLong;

        // ECDAA attestation is not supported (removed in WebAuthn Level 2+).
        if (context.AttStmt.ContainsKey("ecdaaKeyId"))
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "ECDAA attestation is not supported."));
        }

        // Full attestation (x5c present).
        if (context.AttStmt.ContainsKey("x5c"))
        {
            return ValueTask.FromResult(ValidateFullAttestation(context, alg, sig));
        }

        // Self attestation (no x5c, no ecdaaKeyId).
        return ValueTask.FromResult(ValidateSelfAttestation(context, alg, sig));
    }

    private AttestationValidationResult ValidateSelfAttestation(
        AttestationContext context, int alg, byte[] sig)
    {
        // https://www.w3.org/TR/webauthn-3/#sctn-packed-attestation
        if (alg != context.CredentialPublicKey.Algorithm)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.AlgorithmMismatch,
                "Algorithm in attestation statement does not match credential public key algorithm.");
        }

        var signedData = WebAuthnCrypto.CombineBytes([.. context.AuthData], [.. context.ClientDataHash]);

        if (!_signatureVerifiers.TryGetValue(alg, out var verifier))
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.UnsupportedAlgorithm,
                $"Unsupported algorithm {alg} for self-attestation signature verification.");
        }

        if (!verifier.VerifySignature([.. context.CredentialPublicKey.RawCbor], signedData, sig))
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.SignatureVerificationFailed,
                "Self-attestation signature verification failed.");
        }

        return new AttestationValidationResult.Success(null);
    }

    private AttestationValidationResult ValidateFullAttestation(
        AttestationContext context, int alg, byte[] sig)
    {
        if (context.AttStmt["x5c"] is not object?[] x5cArray || x5cArray.Length == 0)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement 'x5c' field is invalid.");
        }

        if (x5cArray[0] is not byte[] certBytes)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidCertificate,
                "Attestation certificate in 'x5c' is not valid DER-encoded data.");
        }

        X509Certificate2 attestationCertificate;
        try
        {
            attestationCertificate = X509CertificateLoader.LoadCertificate(certBytes);
        }
        catch (CryptographicException)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidCertificate,
                "Invalid attestation certificate.");
        }

        using (attestationCertificate)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (now < attestationCertificate.NotBefore.ToUniversalTime() ||
                now > attestationCertificate.NotAfter.ToUniversalTime())
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "Attestation certificate is not within its validity period.");
            }

            var signedData = WebAuthnCrypto.CombineBytes([.. context.AuthData], [.. context.ClientDataHash]);

            if (!_signatureVerifiers.TryGetValue(alg, out var verifier))
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.UnsupportedAlgorithm,
                    $"Unsupported algorithm {alg} for full attestation signature verification.");
            }

            if (!CoseKeyExporter.TryExport(attestationCertificate, alg, out var coseKeyBytes))
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    $"Failed to export public key from attestation certificate for algorithm {alg}.");
            }

            if (!verifier.VerifySignature(coseKeyBytes, signedData, sig))
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.SignatureVerificationFailed,
                    "Full attestation signature verification failed.");
            }

            if (attestationCertificate.Version != RequiredX509Version)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "Attestation certificate must be version 3.");
            }

            var certificateIsValid = ValidateCertificateSubject(attestationCertificate);
            if (!certificateIsValid)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "Attestation certificate subject does not meet requirements. (must contain C, O, OU='Authenticator Attestation', CN).");
            }

            if (attestationCertificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault() is
                { CertificateAuthority: true })
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "Attestation certificate must not be a CA certificate.");
            }

            // AAGUID extension check (optional).
            var aaguidExt = attestationCertificate.Extensions[WebAuthnConstants.Oids.FidoAaguidExtension];
            if (aaguidExt is not null)
            {
                var aaguidError = ValidateAaguidExtension(aaguidExt, [.. context.AuthData]);
                if (aaguidError is not null)
                {
                    return aaguidError;
                }
            }

            if (x5cArray.Any(x => x is not byte[]))
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "Attestation x5c array contains an entry that is not a byte array.");
            }

            var certificateChain = x5cArray.OfType<byte[]>().ToList();

            return new AttestationValidationResult.Success(certificateChain);
        }
    }

    private static bool ValidateCertificateSubject(X509Certificate2 cert)
    {
        // §8.2.1: Subject must contain C, O, OU="Authenticator Attestation", CN.
        var hasCountry = false;
        var hasOrganization = false;
        var hasOrganizationalUnit = false;
        var hasCommonName = false;

        foreach (var rdn in cert.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            // Multi-valued RDNs (e.g. attributes combined with '+') are not expected
            // in WebAuthn attestation certificates. Skip them to avoid
            // InvalidOperationException from GetSingleElementType().
            if (rdn.HasMultipleElements)
            {
                continue;
            }

            var oid = rdn.GetSingleElementType()?.Value;
            if (string.IsNullOrEmpty(oid))
            {
                continue;
            }

            switch (oid)
            {
                case X500Constants.AttributeTypes.Country:
                    hasCountry = true;
                    break;
                case X500Constants.AttributeTypes.Organization:
                    hasOrganization = true;
                    break;
                case X500Constants.AttributeTypes.OrganizationalUnit:
                    var ouValue = rdn.GetSingleElementValue();
                    if (string.Equals(ouValue, "Authenticator Attestation", StringComparison.Ordinal))
                    {
                        hasOrganizationalUnit = true;
                    }

                    break;
                case X500Constants.AttributeTypes.CommonName:
                    hasCommonName = true;
                    break;
            }
        }

        if (!hasCountry || !hasOrganization || !hasOrganizationalUnit || !hasCommonName)
        {
            return false;
        }

        return true;
    }

    private static AttestationValidationResult.Failure? ValidateAaguidExtension(X509Extension aaguidExt,
        byte[] authData)
    {
        var reader = new AsnReader(aaguidExt.RawData, AsnEncodingRules.DER);
        ReadOnlyMemory<byte> certAaguidMemory;
        try
        {
            if (!reader.TryReadPrimitiveOctetString(out certAaguidMemory) || reader.HasData)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.AaguidMismatch,
                    "AAGUID extension in attestation certificate has invalid format.");
            }
        }
        catch (AsnContentException)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.AaguidMismatch,
                "AAGUID extension in attestation certificate has invalid format.");
        }

        var certAaguidBytes = certAaguidMemory.Span;

        if (certAaguidBytes.Length != WebAuthnConstants.AuthenticatorDataLayout.AaguidLength)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.AaguidMismatch,
                "AAGUID extension in attestation certificate has invalid format.");
        }

        // AAGUID in authData is at bytes 37-52 (after rpIdHash[32] + flags[1] + signCount[4]).
        if (authData.Length < AaguidEnd)
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.AaguidMismatch,
                "AuthenticatorData too short to contain AAGUID.");
        }

        var authDataAaguidBytes = authData[WebAuthnConstants.AuthenticatorDataLayout.HeaderLength..AaguidEnd];

        if (!certAaguidBytes.SequenceEqual(authDataAaguidBytes))
        {
            return new AttestationValidationResult.Failure(
                AttestationValidationError.AaguidMismatch,
                "AAGUID in attestation certificate does not match authenticator data.");
        }

        return null;
    }
}
