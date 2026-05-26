// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class WebAuthnRegistrationCeremony(
    IEnumerable<IAttestationFormatValidator> attestationValidators,
    IEnumerable<ISignatureVerifier> signatureVerifiers,
    IEnumerable<IAttestationTrustPolicy> attestationTrustPolicies,
    IOptions<UserAuthenticationOptions> options,
    IPasskeyOriginValidator originValidator)
{
    private readonly PasskeyOptions _passkeyOptions = options.Value.Passkeys;

    private readonly string? _serverDomain =
        string.IsNullOrWhiteSpace(options.Value.Passkeys.ServerDomain)
            ? null
            : options.Value.Passkeys.ServerDomain;

    private IReadOnlyList<string> AllowedOrigins => _passkeyOptions.AllowedOrigins ?? [];

    private readonly Dictionary<string, IAttestationFormatValidator> _validators =
        attestationValidators.ToDictionary(v => v.Format);

    private readonly Dictionary<int, ISignatureVerifier> _signatureVerifiers =
        signatureVerifiers.ToDictionary(v => v.Algorithm);

    private bool IsAlgorithmSupported(int algorithm) =>
        _passkeyOptions.SupportedAlgorithms is { Count: > 0 } configuredAlgorithms
            ? configuredAlgorithms.Contains(algorithm) && _signatureVerifiers.ContainsKey(algorithm)
            : _signatureVerifiers.ContainsKey(algorithm);

    internal List<PublicKeyCredentialParameters> GetPubKeyCredParams()
    {
        if (_passkeyOptions.SupportedAlgorithms is { Count: > 0 } configured)
        {
            return configured
                .Where(alg => _signatureVerifiers.ContainsKey(alg))
                .Distinct()
                .Select(alg => new PublicKeyCredentialParameters { Alg = alg })
                .ToList();
        }

        return signatureVerifiers
            .Select(v => new PublicKeyCredentialParameters { Alg = v.Algorithm })
            .ToList();
    }

    // https://www.w3.org/TR/webauthn-3/#sctn-registering-a-new-credential
    // 7.1
    internal async ValueTask<PasskeyRegistrationCompleteResult> VerifyAttestationAsync(
        PasskeyCompleteRegistrationRequest request,
        string expectedChallenge,
        UserSubjectId userSubjectId,
        DateTimeOffset now,
        Ct ct)
    {
        // 7.1.5 - Parse clientDataJSON
        if (!Base64UrlExtensions.TryDecode(request.Response.ClientDataJSON, out var clientDataJsonBytes))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidClientData,
                $"{nameof(request.Response.ClientDataJSON)} is not valid Base64Url.");
        }

        if (!ClientDataJson.TryParse(clientDataJsonBytes, out var clientData))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidClientData,
                $"{nameof(request.Response.ClientDataJSON)} is malformed.");
        }

        // 7.1.9 - Validate origin
        var validationContext = new PasskeyOriginValidationContext
        {
            Origin = clientData.Origin,
            CrossOrigin = clientData.CrossOrigin ?? false,
            AllowedOrigins = AllowedOrigins
        };
        var isOriginValid = await originValidator.ValidateAsync(validationContext);

        // Validate credential type
        if (request.Type != PasskeyConstants.CredentialType.PublicKey)
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidCredentialType,
                $"Expected credential type 'public-key', got '{request.Type}'.");
        }

        // 7.1.7
        if (clientData.Type != PasskeyConstants.ClientDataType.Create)
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidType,
                $"Expected type 'webauthn.create', got '{clientData.Type}'.");
        }

        // 7.1.8 - Use fixed-time comparison to mitigate timing side-channels.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(clientData.Challenge),
                Encoding.UTF8.GetBytes(expectedChallenge)))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.ChallengeMismatch,
                "Challenge does not match.");
        }

        // 7.1.9 - Validate origin
        if (!isOriginValid)
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.OriginMismatch,
                AllowedOrigins.Count == 0
                    ? $"Origin validation failed because {nameof(PasskeyOptions.AllowedOrigins)} is not configured. Configure at least one allowed origin to enable passkey registration."
                    : $"Origin '{clientData.Origin}' does not match any valid entry in the configured {nameof(PasskeyOptions.AllowedOrigins)}.");
        }

        if (!Base64UrlExtensions.TryDecode(request.Response.AttestationObject, out var attestationObjectBytes))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidAttestationObject,
                $"{nameof(request.Response.AttestationObject)} is not valid Base64Url.");
        }

        // 7.1.12
        if (!AttestationObject.TryParse(attestationObjectBytes, out var attestationObject))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidAttestationObject,
                $"{nameof(request.Response.AttestationObject)} is malformed CBOR.");
        }

        if (!AuthenticatorData.TryParse(attestationObject.AuthData, out var authenticatorData))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidAttestationObject,
                "AuthenticatorData is malformed.");
        }

        // 7.1.13
        var relyingPartyId = _serverDomain ?? new Uri(clientData.Origin).Host;
        var expectedRelyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId));
        if (!CryptographicOperations.FixedTimeEquals(authenticatorData.RpIdHash, expectedRelyingPartyIdHash))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.RpIdMismatch,
                "RP ID hash does not match.");
        }

        // 7.1.14
        if (!authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.UserPresent))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.UserNotPresent,
                "User presence flag not set.");
        }

        // 7.1.15
        if (_passkeyOptions.UserVerificationRequirement == PasskeyConstants.UserVerificationRequirement.Required &&
            !authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.UserVerified))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.UserVerificationRequired,
                "User verification required but not performed.");
        }

        // 7.1.18: If BE is not set, BS must not be set
        if (!authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackupEligible) &&
            authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackedUp))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidBackupState,
                "Backup state (BS) flag is set but backup eligibility (BE) flag is not set.");
        }

        if (authenticatorData.AttestedCredential is null)
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.InvalidAttestationObject,
                "Attested credential data is missing.");
        }

        if (!_validators.TryGetValue(attestationObject.Format, out var validator))
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.UnsupportedAttestationFormat,
                $"Attestation format '{attestationObject.Format}' is not supported.");
        }

        var clientDataHash = SHA256.HashData(clientDataJsonBytes);
        var attestationContext = new AttestationContext(
            attestationObject.AttStmt,
            attestationObject.AuthData,
            clientDataHash,
            authenticatorData.AttestedCredential.PublicKey);

        // Validate Attestation according to Format
        // https://www.w3.org/TR/webauthn-3/#sctn-registering-a-new-credential
        var attestationResult = await validator.ValidateAsync(attestationContext, ct);
        if (attestationResult is AttestationValidationResult.Failure failure)
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.AttestationValidationFailed,
                failure.ErrorDescription);
        }

        var certificateChain = (attestationResult as AttestationValidationResult.Success)?.CertificateChain;
        var trustContext = new AttestationTrustContext
        {
            UserSubjectId = userSubjectId,
            Aaguid = authenticatorData.AttestedCredential.Aaguid,
            AttestationFormat = attestationObject.Format,
            CertificateChain = certificateChain
        };

        var attestationCredential = authenticatorData.AttestedCredential;
        var algorithm = attestationCredential.PublicKey.Algorithm;

        var isAlgorithmSupported = IsAlgorithmSupported(algorithm);
        if (!isAlgorithmSupported)
        {
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.UnsupportedAlgorithm,
                $"Algorithm {algorithm} is not supported.");
        }

        // Evaluate attestation trust policies. If a single one fails, we bail
        foreach (var policy in attestationTrustPolicies)
        {
            var trustResult = await policy.EvaluateAsync(trustContext, ct);
            if (trustResult is AttestationTrustPolicyResult.Rejected trustFailure)
            {
                return new PasskeyRegistrationCompleteResult.Failure(
                    RegistrationError.AttestationTrustPolicyFailed,
                    trustFailure.Reason);
            }
        }

        var credentialData = new PasskeyCredentialData(
            attestationCredential.CredentialId,
            attestationCredential.PublicKey.RawCbor,
            attestationCredential.PublicKey.Algorithm,
            authenticatorData.SignCount,
            authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackupEligible),
            authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackedUp),
            attestationCredential.Aaguid,
            now,
            request.Name);

        return new PasskeyRegistrationCompleteResult.Success(credentialData);
    }
}
