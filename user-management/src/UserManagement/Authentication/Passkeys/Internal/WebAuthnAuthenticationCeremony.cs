// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class WebAuthnAuthenticationCeremony(
    IOptions<UserAuthenticationOptions> options,
    IEnumerable<ISignatureVerifier> signatureVerifiers,
    IPasskeyOriginValidator originValidator)
{
    private readonly PasskeyOptions _passkeyOptions = options.Value.Passkeys;

    private readonly string? _serverDomain =
        string.IsNullOrWhiteSpace(options.Value.Passkeys.ServerDomain)
            ? null
            : options.Value.Passkeys.ServerDomain;

    private IReadOnlyList<string> AllowedOrigins => _passkeyOptions.AllowedOrigins ?? [];

    private readonly Dictionary<int, ISignatureVerifier> _signatureVerifiers =
        signatureVerifiers.ToDictionary(v => v.Algorithm);

    // https://www.w3.org/TR/webauthn-3/#sctn-verifying-assertion
    // 7.2
    internal async ValueTask<PasskeyAuthenticationCompleteResult> VerifyAssertionAsync(
        PasskeyCompleteAuthenticationRequest request,
        string expectedChallenge,
        UserSubjectId? expectedUserSubjectId,
        PasskeyCredential credential,
        UserSubjectId credentialOwnerSubjectId)
    {
        // 7.2.8 - Parse clientDataJSON
        if (!Base64UrlExtensions.TryDecode(request.Response.ClientDataJSON, out var clientDataJsonBytes))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidClientData,
                $"{nameof(request.Response.ClientDataJSON)} is not valid Base64Url.");
        }

        if (!ClientDataJson.TryParse(clientDataJsonBytes, out var clientData))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidClientData,
                $"{nameof(request.Response.ClientDataJSON)} is malformed.");
        }

        // 7.2.12 - Validate origin
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
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidCredentialType,
                $"Expected credential type 'public-key', got '{request.Type}'.");
        }

        // 7.2.10
        if (clientData.Type != PasskeyConstants.ClientDataType.Get)
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidType,
                $"Expected type 'webauthn.get', got '{clientData.Type}'.");
        }

        // 7.2.11 - Use fixed-time comparison to mitigate timing side-channels.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(clientData.Challenge),
                Encoding.UTF8.GetBytes(expectedChallenge)))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.ChallengeMismatch,
                "Challenge does not match.");
        }

        // 7.2.12
        if (!isOriginValid)
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.OriginMismatch,
                AllowedOrigins.Count == 0
                    ? $"Origin validation failed because {nameof(PasskeyOptions.AllowedOrigins)} is not configured. Configure at least one allowed origin to enable passkey authentication."
                    : $"Origin '{clientData.Origin}' does not match any valid entry in the configured {nameof(PasskeyOptions.AllowedOrigins)}.");
        }

        // For non-discoverable flow, verify credential belongs to expected user
        if (expectedUserSubjectId is not null &&
            credentialOwnerSubjectId != expectedUserSubjectId)
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.CredentialUserMismatch,
                "Credential does not belong to the expected user.");
        }

        if (!Base64UrlExtensions.TryDecode(request.Response.AuthenticatorData, out var authenticatorDataBytes))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidAuthenticatorData,
                $"{nameof(request.Response.AuthenticatorData)} is not valid Base64Url.");
        }

        if (!AuthenticatorData.TryParse(authenticatorDataBytes, out var authenticatorData))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidAuthenticatorData,
                "AuthenticatorData is malformed.");
        }

        // 7.2.15
        var relyingPartyId = _serverDomain ?? new Uri(clientData.Origin).Host;
        var expectedRelyingPartyIdHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(relyingPartyId));
        if (!CryptographicOperations.FixedTimeEquals(authenticatorData.RpIdHash, expectedRelyingPartyIdHash))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.RpIdMismatch,
                "RP ID hash does not match.");
        }

        // 7.2.16
        if (!authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.UserPresent))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.UserNotPresent,
                "User presence flag not set.");
        }

        // 7.2.17
        if (_passkeyOptions.UserVerificationRequirement == PasskeyConstants.UserVerificationRequirement.Required &&
            !authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.UserVerified))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.UserVerificationRequired,
                "User verification required but not performed.");
        }

        // 7.2.18/19 BE must not change in either direction
        if (credential.BackupEligible != authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackupEligible))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.BackupEligibilityMismatch,
                "Backup eligibility (BE) flag changed since registration.");
        }

        // 7.2.18 If BE is not set, BS must not be set
        if (!authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackupEligible) &&
            authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackedUp))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.BackupEligibilityMismatch,
                "Backup state (BS) flag is set but backup eligibility (BE) flag is not set.");
        }

        // 7.2.22
        if (credential.SignCount != 0 || authenticatorData.SignCount != 0)
        {
            if (authenticatorData.SignCount <= credential.SignCount)
            {
                return new PasskeyAuthenticationCompleteResult.Failure(
                    AuthenticationCompleteError.InvalidSignCount,
                    $"Invalid signature counter. Expected > {credential.SignCount}, got {authenticatorData.SignCount}. Possible credential cloning detected.");
            }
        }

        // 7.2.20
        var clientDataHash = SHA256.HashData(clientDataJsonBytes);
        var signedData = new byte[authenticatorDataBytes.Length + clientDataHash.Length];
        authenticatorDataBytes.CopyTo(signedData.AsSpan());
        clientDataHash.CopyTo(signedData.AsSpan(authenticatorDataBytes.Length));

        if (!Base64UrlExtensions.TryDecode(request.Response.Signature, out var signatureBytes))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidSignature,
                $"{nameof(request.Response.Signature)} is not valid Base64Url.");
        }

        // 7.2.21
        if (!CoseKey.TryParse(credential.PublicKeyCose, out var coseKey))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidAuthenticatorData,
                "Failed to parse COSE key.");
        }

        if (!_signatureVerifiers.TryGetValue(coseKey.Algorithm, out var verifier))
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.UnsupportedAlgorithm,
                $"Algorithm {coseKey.Algorithm} is not supported.");
        }

        var isSignatureValid = verifier.VerifySignature(credential.PublicKeyCose, signedData, signatureBytes);

        if (!isSignatureValid)
        {
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.SignatureVerificationFailed,
                "Signature verification failed.");
        }

        return new PasskeyAuthenticationCompleteResult.Success(
            credentialOwnerSubjectId,
            credential.CredentialId,
            authenticatorData.SignCount,
            authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.UserVerified),
            authenticatorData.Flags.HasFlag(AuthenticatorDataFlags.BackedUp));
    }
}
