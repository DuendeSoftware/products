// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Validates "tpm" attestation format per WebAuthn Level 3 §8.3.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class TpmAttestationFormatValidator : IAttestationFormatValidator
{
    private const int RequiredX509Version = 3;
    private const byte DirectoryNameGeneralNameTag = 0xA4;
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string TpmManufacturerOid = "2.23.133.2.1";
    private const string TpmModelOid = "2.23.133.2.2";
    private const string TpmVersionOid = "2.23.133.2.3";

    private const int AaguidEnd =
        WebAuthnConstants.AuthenticatorDataLayout.HeaderLength +
        WebAuthnConstants.AuthenticatorDataLayout.AaguidLength;

    private readonly Dictionary<int, ISignatureVerifier> _signatureVerifiers;
    private readonly TimeProvider _timeProvider;

    public TpmAttestationFormatValidator(IEnumerable<ISignatureVerifier> signatureVerifiers, TimeProvider timeProvider)
    {
        _signatureVerifiers = signatureVerifiers.ToDictionary(v => v.Algorithm);
        _timeProvider = timeProvider;
    }

    public string Format => PasskeyConstants.AttestationFormat.Tpm;

    public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct)
    {
        // https://www.w3.org/TR/webauthn-3/#sctn-tpm-attestation

        if (!context.AttStmt.TryGetValue("ver", out var verObj) || verObj is not string ver ||
            !string.Equals(ver, "2.0", StringComparison.Ordinal))
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement is missing required 'ver' field or the value is not '2.0'."));
        }

        if (!context.AttStmt.TryGetValue("certInfo", out var certInfoObj) || certInfoObj is not byte[] certInfoBytes)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement is missing required 'certInfo' field."));
        }

        TpmsAttest certInfo;
        try
        {
            certInfo = TpmsAttest.Parse(certInfoBytes);
        }
        catch (FormatException ex)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                $"Attestation statement 'certInfo' field is invalid. {ex.Message}"));
        }

        if (!context.AttStmt.TryGetValue("pubArea", out var pubAreaObj) || pubAreaObj is not byte[] pubAreaBytes)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement is missing required 'pubArea' field."));
        }

        TpmtPublic pubArea;
        try
        {
            pubArea = TpmtPublic.Parse(pubAreaBytes);
        }
        catch (FormatException ex)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                $"Attestation statement 'pubArea' field is invalid. {ex.Message}"));
        }

        // Parse required attStmt fields. CBOR integers are parsed as long.
        if (!context.AttStmt.TryGetValue("alg", out var algObj) || algObj is not long algLong)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement is missing required 'alg' field."));
        }

        if (!context.AttStmt.TryGetValue("sig", out var sigObj) || sigObj is not byte[] sigBytes)
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

        if (!_signatureVerifiers.TryGetValue(alg, out var verifier))
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.UnsupportedAlgorithm,
                $"Unsupported algorithm {alg} for TPM attestation signature verification."));
        }

        // Per WebAuthn §8.3 step 3: "Verify that extraData is set to the hash of attToBeSigned
        // using the hash algorithm employed in 'alg'."
        // The hash algorithm is derived from the COSE algorithm, not from any TPM structure field.
        var expectedTpmHashAlg = TpmStructures.GetTpmHashAlgorithmForCoseAlg(alg);
        if (expectedTpmHashAlg is null)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.UnsupportedAlgorithm,
                $"Cannot determine hash algorithm for COSE algorithm {alg}."));
        }

        byte[] authData = [.. context.AuthData];
        byte[] clientDataHash = [.. context.ClientDataHash];

        if (!TpmStructures.ValidateExtraData(certInfo, authData, clientDataHash, expectedTpmHashAlg.Value))
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "TPM attestation 'extraData' does not match the hash of authData || clientDataHash."));
        }

        if (!TpmStructures.ValidateAttestedName(certInfo, pubArea, pubAreaBytes))
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "TPM attestation 'attested.name' does not match the supplied 'pubArea'."));
        }

        if (!TpmStructures.ValidatePublicKeyMatch(pubArea, context.CredentialPublicKey))
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "TPM attestation 'pubArea.unique' does not match the credential public key."));
        }

        if ((pubArea.ObjectAttributes & TpmConstants.ObjectAttributeSignEncrypt) == 0)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "TPM public key must have the sign/encrypt attribute set."));
        }

        return ValueTask.FromResult(ValidateFullAttestation(context, alg, verifier, certInfoBytes, sigBytes, authData));
    }

    private AttestationValidationResult ValidateFullAttestation(
        AttestationContext context,
        int alg,
        ISignatureVerifier verifier,
        byte[] certInfoBytes,
        byte[] sigBytes,
        byte[] authData)
    {
        if (!context.AttStmt.TryGetValue("x5c", out var x5cObj) || x5cObj is not object?[] x5cArray ||
            x5cArray.Length == 0)
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

            if (!CoseKeyExporter.TryExport(attestationCertificate, alg, out var coseKeyBytes))
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    $"Failed to export public key from attestation certificate for algorithm {alg}.");
            }

            // Per WebAuthn §8.3 step 7: "Verify the sig is a valid signature over certInfo
            // using the attestation public key in aikCert with the algorithm specified in alg."
            // The sig field is passed directly to the signature verifier as opaque bytes.
            if (!verifier.VerifySignature(coseKeyBytes, certInfoBytes, sigBytes))
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.SignatureVerificationFailed,
                    "TPM attestation signature verification failed.");
            }

            if (attestationCertificate.Version != RequiredX509Version)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "Attestation certificate must be version 3.");
            }

            var certificateSubjectError = GetCertificateSubjectError(attestationCertificate);
            if (certificateSubjectError is not null)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    certificateSubjectError);
            }

            var basicConstraintsExtension =
                attestationCertificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
            if (basicConstraintsExtension is null)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "TPM attestation certificate must include Basic Constraints with CA=false.");
            }

            if (basicConstraintsExtension.CertificateAuthority)
            {
                return new AttestationValidationResult.Failure(
                    AttestationValidationError.InvalidCertificate,
                    "TPM attestation certificate must not be a CA certificate.");
            }

            // AAGUID extension check (optional). If present, its value must match authData.
            var aaguidExt = attestationCertificate.Extensions[WebAuthnConstants.Oids.FidoAaguidExtension];
            if (aaguidExt is not null)
            {
                var aaguidError = ValidateAaguidExtension(aaguidExt, authData);
                if (aaguidError is not null)
                {
                    return aaguidError;
                }
            }

            var certificateChain = new List<byte[]>(x5cArray.Length);
            certificateChain.Add(certBytes);

            for (var i = 1; i < x5cArray.Length; i++)
            {
                if (x5cArray[i] is not byte[] chainCertBytes)
                {
                    return new AttestationValidationResult.Failure(
                        AttestationValidationError.InvalidCertificate,
                        $"Attestation x5c array entry at index {i} is not a byte array.");
                }

                X509Certificate2 chainCert;
                try
                {
                    chainCert = X509CertificateLoader.LoadCertificate(chainCertBytes);
                }
                catch (CryptographicException)
                {
                    return new AttestationValidationResult.Failure(
                        AttestationValidationError.InvalidCertificate,
                        $"Attestation x5c array entry at index {i} is not a valid certificate.");
                }

                using (chainCert)
                {
                    if (now < chainCert.NotBefore.ToUniversalTime() ||
                        now > chainCert.NotAfter.ToUniversalTime())
                    {
                        return new AttestationValidationResult.Failure(
                            AttestationValidationError.InvalidCertificate,
                            $"Attestation x5c certificate at index {i} is not within its validity period.");
                    }
                }

                certificateChain.Add(chainCertBytes);
            }

            return new AttestationValidationResult.Success(certificateChain);
        }
    }

    private static bool ContainsRequiredTpmSanFields(byte[] sanRawData)
    {
        var generalNames = ReadSequenceContents(sanRawData);

        while (!generalNames.IsEmpty)
        {
            var generalName = ReadEncodedValue(ref generalNames);
            if (generalName[0] != DirectoryNameGeneralNameTag)
            {
                continue;
            }

            var directoryName = ReadContextSpecificContents(generalName, 4);
            if (DirectoryNameContainsRequiredAttributes(directoryName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DirectoryNameContainsRequiredAttributes(ReadOnlySpan<byte> directoryName)
    {
        var relativeDistinguishedNames = ReadSequenceContents(directoryName);
        var hasManufacturer = false;
        var hasModel = false;
        var hasVersion = false;

        while (!relativeDistinguishedNames.IsEmpty)
        {
            var relativeDistinguishedName = ReadEncodedValue(ref relativeDistinguishedNames);
            var attributeSet = ReadSetContents(relativeDistinguishedName);

            while (!attributeSet.IsEmpty)
            {
                var attribute = ReadEncodedValue(ref attributeSet);
                var attributeSequence = ReadSequenceContents(attribute);
                var attributeType = ReadEncodedValue(ref attributeSequence);
                var oid = AsnDecoder.ReadObjectIdentifier(attributeType, AsnEncodingRules.DER, out _);

                switch (oid)
                {
                    case TpmManufacturerOid:
                        hasManufacturer = AttributeHasNonEmptySupportedStringValue(attributeSequence);
                        break;
                    case TpmModelOid:
                        hasModel = AttributeHasNonEmptySupportedStringValue(attributeSequence);
                        break;
                    case TpmVersionOid:
                        hasVersion = AttributeHasNonEmptySupportedStringValue(attributeSequence);
                        break;
                }
            }
        }

        return hasManufacturer && hasModel && hasVersion;
    }

    private static bool AttributeHasNonEmptySupportedStringValue(ReadOnlySpan<byte> attributeSequence)
    {
        if (attributeSequence.IsEmpty)
        {
            return false;
        }

        var value = ReadEncodedValue(ref attributeSequence);
        if (!attributeSequence.IsEmpty)
        {
            return false;
        }

        var tag = AsnDecoder.ReadEncodedValue(
            value,
            AsnEncodingRules.DER,
            out _,
            out var contentLength,
            out var bytesConsumed);

        if (bytesConsumed != value.Length || tag.TagClass != TagClass.Universal)
        {
            return false;
        }

        if (contentLength == 0)
        {
            return false;
        }

        try
        {
            _ = tag.TagValue switch
            {
                (int)UniversalTagNumber.UTF8String => AsnDecoder.ReadCharacterString(
                    value,
                    AsnEncodingRules.DER,
                    UniversalTagNumber.UTF8String,
                    out _),
                (int)UniversalTagNumber.PrintableString => AsnDecoder.ReadCharacterString(
                    value,
                    AsnEncodingRules.DER,
                    UniversalTagNumber.PrintableString,
                    out _),
                _ => null
            };

            return tag.TagValue is (int)UniversalTagNumber.UTF8String or (int)UniversalTagNumber.PrintableString;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    private static string? GetCertificateSubjectError(X509Certificate2 certificate)
    {
        // §8.3.1: Subject must be empty.
        if (certificate.SubjectName.RawData.Length > 2)
        {
            return "TPM attestation certificate subject field must be empty.";
        }

        // §8.3.1: SAN must contain TPM manufacturer, model, and version.
        if (!HasRequiredSubjectAlternativeNameFields(certificate))
        {
            return "TPM attestation certificate SAN must contain manufacturer, model, and version.";
        }

        // §8.3.1: EKU must contain tcg-kp-AIKCertificate.
        if (!HasRequiredAikExtendedKeyUsage(certificate))
        {
            return $"TPM attestation certificate EKU must contain '{WebAuthnConstants.Oids.TcgKpAikCertificate}'.";
        }

        return null;
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

    private static bool HasRequiredSubjectAlternativeNameFields(X509Certificate2 certificate)
    {
        var sanExtension = certificate.Extensions[SubjectAlternativeNameOid];
        if (sanExtension is null)
        {
            return false;
        }

        try
        {
            return ContainsRequiredTpmSanFields(sanExtension.RawData);
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    private static bool HasRequiredAikExtendedKeyUsage(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(extension => extension.EnhancedKeyUsages.Cast<Oid>())
            .Any(oid => string.Equals(oid.Value, WebAuthnConstants.Oids.TcgKpAikCertificate, StringComparison.Ordinal));

    private static ReadOnlySpan<byte> ReadSequenceContents(ReadOnlySpan<byte> encodedValue)
    {
        AsnDecoder.ReadSequence(encodedValue, AsnEncodingRules.DER, out var contentOffset, out var contentLength,
            out var bytesConsumed);

        if (bytesConsumed != encodedValue.Length)
        {
            throw new AsnContentException();
        }

        return encodedValue.Slice(contentOffset, contentLength);
    }

    private static ReadOnlySpan<byte> ReadSetContents(ReadOnlySpan<byte> encodedValue)
    {
        AsnDecoder.ReadSetOf(encodedValue, AsnEncodingRules.DER, out var contentOffset, out var contentLength,
            out var bytesConsumed);

        if (bytesConsumed != encodedValue.Length)
        {
            throw new AsnContentException();
        }

        return encodedValue.Slice(contentOffset, contentLength);
    }

    private static ReadOnlySpan<byte> ReadContextSpecificContents(ReadOnlySpan<byte> encodedValue, int tagValue)
    {
        var expectedTag = new Asn1Tag(TagClass.ContextSpecific, tagValue, isConstructed: true);
        var actualTag = AsnDecoder.ReadEncodedValue(
            encodedValue,
            AsnEncodingRules.DER,
            out var contentOffset,
            out var contentLength,
            out var bytesConsumed);

        if (actualTag != expectedTag)
        {
            throw new AsnContentException();
        }

        if (bytesConsumed != encodedValue.Length)
        {
            throw new AsnContentException();
        }

        return encodedValue.Slice(contentOffset, contentLength);
    }

    private static ReadOnlySpan<byte> ReadEncodedValue(ref ReadOnlySpan<byte> remaining)
    {
        _ = AsnDecoder.ReadEncodedValue(remaining, AsnEncodingRules.DER, out _, out _, out var bytesConsumed);
        var encodedValue = remaining[..bytesConsumed];
        remaining = remaining[bytesConsumed..];
        return encodedValue;
    }
}
