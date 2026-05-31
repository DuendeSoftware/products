// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Passkeys.Internal.Storage;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PasskeyCeremonies(
    IPasskeyAuthenticationChallengeStore authChallengeStore,
    IPasskeyRegistrationChallengeStore regChallengeStore,
    UserAuthenticatorsRepository userAuthenticatorsRepository,
    WebAuthnAuthenticationCeremony webAuthnAuthenticationCeremony,
    WebAuthnRegistrationCeremony webAuthnRegistrationCeremony,
    IOptions<UserAuthenticationOptions> options,
    TimeProvider timeProvider,
    ILogger<PasskeyCeremonies> logger,
    UserManagementLicenseValidator licenseValidator) : IPasskeyCeremonies
{
    private readonly PasskeyOptions _passkeyOptions = options.Value.Passkeys;

    private readonly string? _serverDomain =
        string.IsNullOrWhiteSpace(options.Value.Passkeys.ServerDomain)
            ? null
            : options.Value.Passkeys.ServerDomain;

    public async Task<PasskeyRegistrationSession> BeginRegistrationAsync(
        UserSubjectId userSubjectId,
        string userName,
        string userDisplayName,
        Ct ct)
    {
        licenseValidator.ValidatePasskey();
        using var scope = logger.BeginSubjectScope(userSubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var excludeCredentials = await userAuthenticatorsRepository.TryReadAsync(userSubjectId, ct) is (var user, _)
            ? user.PasskeyCredentials.Keys
                .Select(id => new PublicKeyCredentialDescriptor
                {
                    Id = Base64Url.EncodeToString(id.ToBytes())
                })
                .ToList()
            : [];

        var challenge = WebAuthnCrypto.GenerateChallenge(_passkeyOptions.ChallengeSize);

        var passkeyRegistrationChallenge = PasskeyRegistrationChallenge.Create(
            challenge,
            userSubjectId,
            timeProvider.GetUtcNow());

        _ = await regChallengeStore.CreateAsync(passkeyRegistrationChallenge, ct);

        /*
         * WebAuthn 5.4.3 requires user handles to be 1–64 bytes.
         * UserSubjectId can be up to 200 characters, so we hash it with SHA-256
         * to produce a fixed 32-byte opaque handle that is stable, unique,
         * and always within the spec limit.
         */
        var hashedSubjectId = SHA256.HashData(Encoding.UTF8.GetBytes(userSubjectId.Value));

        var creationOptions = new PublicKeyCredentialCreationOptions
        {
            Challenge = challenge,
            RelyingParty = new PublicKeyCredentialRelyingPartyEntity
            {
                Id = _serverDomain,
                Name = _passkeyOptions.RelyingPartyName
            },
            User = new PublicKeyCredentialUserEntity
            {
                Id = Base64Url.EncodeToString(hashedSubjectId),
                Name = userName,
                DisplayName = userDisplayName
            },
            PubKeyCredParams = webAuthnRegistrationCeremony.GetPubKeyCredParams(),
            Attestation = _passkeyOptions.AttestationConveyancePreference,
            AuthenticatorSelection = new AuthenticatorSelectionCriteria
            {
                AuthenticatorAttachment = _passkeyOptions.AuthenticatorAttachment,
                UserVerification = _passkeyOptions.UserVerificationRequirement,
                ResidentKey = _passkeyOptions.ResidentKeyRequirement
            },
            ExcludeCredentials = excludeCredentials,
            Timeout = (uint)_passkeyOptions.ChallengeTimeout.TotalMilliseconds
        };

        return new PasskeyRegistrationSession(passkeyRegistrationChallenge.Id, creationOptions);
    }

    public async Task<PasskeyRegistrationCompleteResult> CompleteRegistrationAsync(
        PasskeyCompleteRegistrationRequest request,
        Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var challengeId = PasskeyRegistrationChallengeId.From(request.ChallengeId);
        var challenge = await regChallengeStore.TryReadAsync(challengeId, ct);
        if (challenge is null)
        {
            logger.PasskeyRegistrationChallengeNotFound(LogLevel.Information);
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.ChallengeNotFound,
                "Challenge not found.");
        }

        using var scope = logger.BeginSubjectScope(challenge.UserSubjectId);

        // Immediately delete the challenge to enforce single-use (prevent replay attacks).
        var deleteResult = await regChallengeStore.DeleteAsync(challengeId, ct);
        if (deleteResult != DeleteResult.Success)
        {
            logger.FailedToDeletePasskeyRegistrationChallenge(LogLevel.Warning, challengeId, deleteResult);
        }

        var now = timeProvider.GetUtcNow();

        // Check if the challenge has expired (policy check, not a WebAuthn spec step).
        if (challenge.CreatedAt + _passkeyOptions.ChallengeTimeout < now)
        {
            logger.PasskeyRegistrationChallengeExpired(LogLevel.Information, challengeId);
            return new PasskeyRegistrationCompleteResult.Failure(
                RegistrationError.ChallengeExpired,
                "Challenge has expired.");
        }

        var registrationResult = await webAuthnRegistrationCeremony.VerifyAttestationAsync(
            request,
            challenge.Challenge,
            challenge.UserSubjectId,
            now,
            ct);

        if (registrationResult is PasskeyRegistrationCompleteResult.Success)
        {
            logger.PasskeyRegisterCompleteSucceeded(LogLevel.Information, challenge.UserSubjectId);
        }

        return registrationResult;
    }

    public async Task<PasskeyAuthenticationBeginResult> BeginAuthenticationAsync(Ct ct)
    {
        licenseValidator.ValidatePasskey();
        var challenge = WebAuthnCrypto.GenerateChallenge(_passkeyOptions.ChallengeSize);

        var passkeyAuthenticationChallenge = PasskeyAuthenticationChallenge.CreateDiscoverable(
            challenge,
            timeProvider.GetUtcNow());

        _ = await authChallengeStore.CreateAsync(passkeyAuthenticationChallenge, ct);

        var requestOptions = new PublicKeyCredentialRequestOptions
        {
            Challenge = challenge,
            RpId = _serverDomain,
            AllowCredentials = [],
            UserVerification = _passkeyOptions.UserVerificationRequirement,
            Timeout = (uint)_passkeyOptions.ChallengeTimeout.TotalMilliseconds
        };

        var session = new PasskeyAuthenticationSession(passkeyAuthenticationChallenge.Id, requestOptions);
        return new PasskeyAuthenticationBeginResult.Success(session);
    }

    public async Task<PasskeyAuthenticationBeginResult> BeginAuthenticationAsync(
        UserSubjectId userSubjectId,
        Ct ct)
    {
        licenseValidator.ValidatePasskey();
        using var scope = logger.BeginSubjectScope(userSubjectId);
        var challenge = WebAuthnCrypto.GenerateChallenge(_passkeyOptions.ChallengeSize);

        var passkeyAuthenticationChallenge = PasskeyAuthenticationChallenge.Create(
            challenge,
            userSubjectId,
            timeProvider.GetUtcNow());

        var record = await userAuthenticatorsRepository.TryReadAsync(userSubjectId, ct);

        var userAuthenticators = record?.UserAuthenticators;

        var passkeyCredentials = userAuthenticators?.PasskeyCredentials;
        if (passkeyCredentials == null || passkeyCredentials.Count == 0)
        {
            logger.PasskeyAuthenticationNoPasskeysRegistered(LogLevel.Information, userSubjectId);
            return new PasskeyAuthenticationBeginResult.Failure(AuthenticationBeginError.NoPasskeyRegistered,
                "No passkey registered.");
        }

        var allowCredentials = passkeyCredentials.Keys
            .Select(id => new PublicKeyCredentialDescriptor
            {
                Id = Base64Url.EncodeToString(id.ToBytes())
            })
            .ToList();

        _ = await authChallengeStore.CreateAsync(passkeyAuthenticationChallenge, ct);

        var requestOptions = new PublicKeyCredentialRequestOptions
        {
            Challenge = challenge,
            RpId = _serverDomain,
            AllowCredentials = allowCredentials,
            UserVerification = _passkeyOptions.UserVerificationRequirement,
            Timeout = (uint)_passkeyOptions.ChallengeTimeout.TotalMilliseconds
        };

        var session = new PasskeyAuthenticationSession(passkeyAuthenticationChallenge.Id, requestOptions);
        return new PasskeyAuthenticationBeginResult.Success(session);
    }

    public async Task<PasskeyAuthenticationCompleteResult> CompleteAuthenticationAsync(
        PasskeyCompleteAuthenticationRequest request,
        Ct ct)
    {
        licenseValidator.ValidatePasskey();
        ArgumentNullException.ThrowIfNull(request);

        var challengeId = PasskeyAuthenticationChallengeId.From(request.ChallengeId);
        var challenge = await authChallengeStore.TryReadAsync(challengeId, ct);
        if (challenge is null)
        {
            logger.PasskeyAuthenticationChallengeNotFound(LogLevel.Information);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.ChallengeNotFound,
                "Challenge not found.");
        }

        // Immediately delete the challenge to enforce single-use (prevent replay attacks).
        var deleteResult = await authChallengeStore.DeleteAsync(challengeId, ct);
        if (deleteResult != DeleteResult.Success)
        {
            logger.FailedToDeletePasskeyAuthenticationChallenge(LogLevel.Warning, challengeId, deleteResult);
        }

        // Check if the challenge has expired.
        var now = timeProvider.GetUtcNow();
        if (challenge.CreatedAt + _passkeyOptions.ChallengeTimeout < now)
        {
            logger.PasskeyAuthenticationChallengeExpired(LogLevel.Information, challengeId);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.ChallengeExpired,
                "Challenge has expired.");
        }

        // Look up credential by ID from the request
        if (!Base64UrlExtensions.TryDecode(request.RawId, out var credentialIdBytes))
        {
            logger.PasskeyAuthenticationInvalidCredentialIdEncoding(LogLevel.Information);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidClientData,
                $"{nameof(request.RawId)} is not valid Base64Url.");
        }

        if (!PasskeyCredentialId.TryFrom(credentialIdBytes, out var credentialId))
        {
            logger.PasskeyAuthenticationInvalidCredentialId(LogLevel.Information);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.InvalidClientData,
                $"{nameof(request.RawId)} is not a valid credential ID.");
        }

        var foundUser = await userAuthenticatorsRepository.TryReadAsync(credentialId.Value, ct);
        if (foundUser is null)
        {
            logger.PasskeyAuthenticationCredentialNotFound(LogLevel.Information, credentialId.Value);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.CredentialNotFound,
                "Credential not found.");
        }

        var (user, userVersion) = foundUser.Value;

        var credential = user.TryGet(credentialId.Value);
        if (credential is null)
        {
            logger.PasskeyAuthenticationCredentialNotOnUser(LogLevel.Information, credentialId.Value);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.CredentialNotFound,
                "Credential not found on user.");
        }

        var result = await webAuthnAuthenticationCeremony.VerifyAssertionAsync(
            request,
            challenge.Challenge,
            challenge.UserSubjectId,
            credential,
            user.SubjectId);

        if (result is not PasskeyAuthenticationCompleteResult.Success success)
        {
            return result;
        }

        _ = user.TryUpdateSignCount(credentialId.Value, success.SignCount);
        _ = user.TryUpdateBackedUp(credentialId.Value, success.BackedUp);
        var updateResult = await userAuthenticatorsRepository.UpdateAsync(user, userVersion, ct);
        if (updateResult != UpdateResult.Success)
        {
            logger.PasskeyAuthenticationSignCountUpdateFailed(LogLevel.Warning, credentialId.Value);
            return new PasskeyAuthenticationCompleteResult.Failure(
                AuthenticationCompleteError.StorageError,
                "Failed to update user with updated passkey sign count and backup state.");
        }

        return result;
    }
}
