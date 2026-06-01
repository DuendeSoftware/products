// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using Duende.Platform.UserManagement.Passkeys.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class PasskeyAuthentication : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasskeyCeremonies _ceremonies = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _ceremonies = _serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task BeginAsync_with_user_with_Passkey_returns_success()
    {
        var (userSubjectId, _, _) = await CreateUserWithPasskey();

        var result = await _ceremonies.BeginAuthenticationAsync(userSubjectId, _ct);

        _ = result.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var success = (PasskeyAuthenticationBeginResult.Success)result;
        success.Session.ChallengeId.ShouldNotBe(Guid.Empty);
        _ = success.Session.Options.ShouldNotBeNull();
        success.Session.Options.Challenge.ShouldNotBeNullOrWhiteSpace();
        _ = success.Session.Options.AllowCredentials.ShouldHaveSingleItem();
        success.Session.Options.UserVerification.ShouldBe(PasskeyConstants.UserVerificationRequirement.Preferred);
    }

    [Fact]
    public async Task Challenge_should_be_unique()
    {
        var result1 = await _ceremonies.BeginAuthenticationAsync(_ct);
        var result2 = await _ceremonies.BeginAuthenticationAsync(_ct);

        var success1 = (PasskeyAuthenticationBeginResult.Success)result1;
        var success2 = (PasskeyAuthenticationBeginResult.Success)result2;
        success1.Session.Options.Challenge.ShouldNotBe(success2.Session.Options.Challenge);
    }

    [Fact]
    public async Task ChallengeId_should_be_unique()
    {
        var result1 = await _ceremonies.BeginAuthenticationAsync(_ct);
        var result2 = await _ceremonies.BeginAuthenticationAsync(_ct);

        var success1 = (PasskeyAuthenticationBeginResult.Success)result1;
        var success2 = (PasskeyAuthenticationBeginResult.Success)result2;
        success1.Session.ChallengeId.ShouldNotBe(success2.Session.ChallengeId);
    }

    [Fact]
    public async Task AllowCredentials_should_contain_user_credential()
    {
        var (userSubjectId, credentialId, _) = await CreateUserWithPasskey();

        var result = await _ceremonies.BeginAuthenticationAsync(userSubjectId, _ct);

        var success = (PasskeyAuthenticationBeginResult.Success)result;
        _ = success.Session.Options.AllowCredentials.ShouldHaveSingleItem();
        var expectedCredentialIdBase64Url = Base64Url.EncodeToString(credentialId);
        success.Session.Options.AllowCredentials[0].Id.ShouldBe(expectedCredentialIdBase64Url);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public async Task Challenge_size_should_be_configurable(int challengeSize)
    {
        await using var serviceProvider = await UsersServiceProviderFactory
            .CreateWithOptionsAsync(options => options.Passkeys.ChallengeSize = challengeSize);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();

        var result = await passkeyAuth.BeginAuthenticationAsync(_ct);

        var success = (PasskeyAuthenticationBeginResult.Success)result;
        var challengeBytes = WebAuthnFixtures.DecodeBase64Url(success.Session.Options.Challenge);
        challengeBytes.Length.ShouldBe(challengeSize);
    }

    [Fact]
    public async Task CompleteAsync_with_invalid_ChallengeId_returns_challenge_not_found()
    {
        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: Guid.CreateVersion7(),
            credentialId: [1, 2, 3, 4],
            clientDataJson: Base64Url.EncodeToString("{}"u8),
            authenticatorData: [0, 0, 0, 0],
            signature: Base64Url.EncodeToString([1, 2, 3]));

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.ChallengeNotFound);
    }

    [Fact]
    public async Task CompleteAsync_with_invalid_Base64Url_ClientData_returns_invalid_ClientData()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: "invalid-base-64-url",
            authenticatorData: [0, 0, 0, 0],
            signature: Base64Url.EncodeToString([1, 2, 3]));

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.InvalidClientData);
    }

    [Fact]
    public async Task CompleteAsync_with_malformed_ClientDataJson_returns_invalid_ClientData()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: Base64Url.EncodeToString("invalid-json"u8),
            authenticatorData: [0, 0, 0, 0],
            signature: Base64Url.EncodeToString([1, 2, 3]));

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.InvalidClientData);
    }

    [Fact]
    public async Task CompleteAsync_with_invalid_CredentialType_returns_invalid_CredentialType()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            success.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = new PasskeyCompleteAuthenticationRequest
        {
            ChallengeId = success.Session.ChallengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = "invalid-type",
            Response = new AuthenticatorAssertionResponse
            {
                ClientDataJSON = clientData,
                AuthenticatorData = Base64Url.EncodeToString(authenticatorData),
                Signature = WebAuthnFixtures.CreateInvalidSignature()
            }
        };

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.InvalidCredentialType);
    }

    [Fact]
    public async Task CompleteAsync_with_invalid_type_returns_invalid_type()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create,
            success.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.InvalidType);
    }

    [Fact]
    public async Task CompleteAsync_with_challenge_mismatch_returns_challenge_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            "mismatched-challenge", "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.ChallengeMismatch);
    }

    [Fact]
    public async Task CompleteAsync_with_origin_mismatch_returns_origin_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://evil.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAsync_with_invalid_AuthenticatorData_returns_invalid_AuthenticatorData()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            success.Session.Options.Challenge, "https://example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: [0], // Invalid authenticator data
            signature: Base64Url.EncodeToString([1, 2, 3]));

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.InvalidAuthenticatorData);
    }

    [Fact]
    public async Task CompleteAsync_with_RpId_mismatch_returns_RpId_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            success.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("wrong.com"); // Wrong RP ID

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: Base64Url.EncodeToString([1, 2, 3]));

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.RpIdMismatch);
    }

    [Fact]
    public async Task CompleteAsync_with_user_not_present_returns_user_not_present()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            success.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x00); // No UP flag

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: Base64Url.EncodeToString([1, 2, 3]));

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.UserNotPresent);
    }

    [Fact]
    public async Task CompleteAsync_with_invalid_signature_returns_signature_verification_failed()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var success = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            success.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", signCount: 1);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: success.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.SignatureVerificationFailed);
    }

    [Fact]
    public async Task CompleteAsync_with_valid_signature_returns_success()
    {
        var (userSubjectId, credentialId, ecdsa) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        _ = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        var completeSuccess = (PasskeyAuthenticationCompleteResult.Success)result;
        completeSuccess.UserSubjectId.ShouldBe(userSubjectId);
        completeSuccess.SignCount.ShouldBe(1u);
        completeSuccess.UserVerified.ShouldBeFalse();
        completeSuccess.BackedUp.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteAsync_with_IeeeP1363_signature_returns_signature_verification_failed()
    {
        var (_, credentialId, ecdsa) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        _ = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.SignatureVerificationFailed);
    }

    [Fact]
    public async Task Challenge_is_deleted_after_successful_authentication()
    {
        var (_, credentialId, ecdsa) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var successResult = await _ceremonies.CompleteAuthenticationAsync(request, _ct);
        _ = successResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();

        var failedResult = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = failedResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.ChallengeNotFound);
    }

    [Fact]
    public async Task Challenge_is_deleted_after_failed_authentication()
    {
        var (_, credentialId, ecdsa) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var failedResult = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = failedResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.SignatureVerificationFailed);

        // Verify challenge was deleted: a second attempt should return ChallengeNotFound
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var validSignature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);
        var secondRequest = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: validSignature);

        var secondResult = await _ceremonies.CompleteAuthenticationAsync(secondRequest, _ct);
        var secondFailure = secondResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        secondFailure.Error.ShouldBe(AuthenticationCompleteError.ChallengeNotFound);
    }

    [Fact]
    public async Task Passkey_can_be_used_for_multiple_authentications()
    {
        var (userSubjectId, credentialId, ecdsa) = await CreateUserWithPasskey();

        var beginFirstAuthenticationResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var firstAuthenticationResult =
            beginFirstAuthenticationResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var firstClientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            firstAuthenticationResult.Session.Options.Challenge, "https://example.com");
        var firstAuthenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var firstClientDataBytes = WebAuthnFixtures.DecodeBase64Url(firstClientData);
        var firstSignature = WebAuthnFixtures.CreateValidSignature(ecdsa, firstAuthenticatorData, firstClientDataBytes);

        var firstRequest = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: firstAuthenticationResult.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: firstClientData,
            authenticatorData: firstAuthenticatorData,
            signature: firstSignature);

        var firstCompletionResult =
            await _ceremonies.CompleteAuthenticationAsync(firstRequest, _ct);

        var firstSuccess = firstCompletionResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        firstSuccess.UserSubjectId.ShouldBe(userSubjectId);
        firstSuccess.SignCount.ShouldBe(1u);

        // Second authentication - should also succeed (verifies keys weren't cleared by update)
        var beginSecondAuthenticationResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var secondAuthenticationResult =
            beginSecondAuthenticationResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var secondClientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            secondAuthenticationResult.Session.Options.Challenge, "https://example.com");
        var secondAuthenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 2);
        var secondClientDataBytes = WebAuthnFixtures.DecodeBase64Url(secondClientData);
        var secondSignature =
            WebAuthnFixtures.CreateValidSignature(ecdsa, secondAuthenticatorData, secondClientDataBytes);

        var secondRequest = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: secondAuthenticationResult.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: secondClientData,
            authenticatorData: secondAuthenticatorData,
            signature: secondSignature);

        var secondCompletionResult =
            await _ceremonies.CompleteAuthenticationAsync(secondRequest, _ct);
        var secondSuccess = secondCompletionResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        secondSuccess.UserSubjectId.ShouldBe(userSubjectId);
        secondSuccess.SignCount.ShouldBe(2u);
    }

    [Fact]
    public async Task Discoverable_auth_should_return_empty_AllowCredentials()
    {
        var result = await _ceremonies.BeginAuthenticationAsync(_ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        success.Session.Options.AllowCredentials.ShouldBeEmpty();
        success.Session.Options.Challenge.ShouldNotBeNullOrWhiteSpace();
        success.Session.ChallengeId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task BeginAsync_with_user_without_Passkey_returns_no_passkey_registered()
    {
        var subjectId =
            (await _externalAuthenticator.TryAuthenticateAsync(TestData.CreateExternalAuthenticatorAddress(), _ct))
            .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        var result = await _ceremonies.BeginAuthenticationAsync(subjectId, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationBeginResult.Failure>();
        failure.Error.ShouldBe(AuthenticationBeginError.NoPasskeyRegistered);
    }

    [Fact]
    public async Task Discoverable_auth_should_succeed_with_valid_credential()
    {
        var (userSubjectId, credentialId, ecdsa) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        beginSuccess.Session.Options.AllowCredentials.ShouldBeEmpty();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var completeSuccess = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        completeSuccess.UserSubjectId.ShouldBe(userSubjectId);
        completeSuccess.SignCount.ShouldBe(1u);
    }

    [Fact]
    public async Task Discoverable_auth_should_fail_with_unknown_credential()
    {
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var unknownCredentialId = RandomNumberGenerator.GetBytes(32);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: unknownCredentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.CredentialNotFound);
    }

    [Fact]
    public async Task Discoverable_auth_should_fail_with_invalid_signature()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.SignatureVerificationFailed);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_with_cross_origin_request_returns_origin_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJsonWithCrossOrigin(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://evil.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_with_empty_origin_returns_origin_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_with_invalid_uri_origin_returns_origin_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "not-a-valid-uri");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_with_scheme_mismatch_returns_origin_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "http://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_with_port_mismatch_returns_origin_mismatch()
    {
        var (_, credentialId, _) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com:8080");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com");

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_with_explicit_default_port_succeeds()
    {
        var (_, credentialId, ecdsa) = await CreateUserWithPasskey();
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;

        // Client sends :443 explicitly, expected is https://example.com (implicit 443)
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge,
            "https://example.com:443");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        // Assert — :443 is the default HTTPS port, so this should succeed
        _ = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
    }

    [Fact]
    public async Task When_ServerDomain_null_uses_origin_as_RelyingPartyId()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = null);
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var externalAuthenticator = serviceProvider.GetRequiredService<IExternalAuthenticator>();
        var (_, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: externalAuthenticator);

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
    }

    [Fact]
    public async Task BeginAsync_sets_RelyingPartyId_when_ServerDomain_configured()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = "example.com");

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();

        var result = await passkeyAuth.BeginAuthenticationAsync(_ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        success.Session.Options.RpId.ShouldBe("example.com");
    }

    [Fact]
    public async Task Uses_ServerDomain_as_origin_as_RelyingPartyId_when_configured()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = "example.com");
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var (_, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;

        // RP ID should be the configured ServerDomain (example.com)
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
    }

    [Fact]
    public async Task Fails_when_received_subdomain_does_not_match_configured_ServerDomain()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = "example.com");
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var (_, credentialId, _) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = (PasskeyAuthenticationBeginResult.Success)beginResult;
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge,
            "https://app1.example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: WebAuthnFixtures.CreateInvalidSignature());

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task Custom_SignatureVerifier_can_be_registered_and_used()
    {
        const int edDsaAlgorithm = -8;

        await using var serviceProvider = await UsersServiceProviderFactory
            .CreateUsersBuilderAsync(
                options => options.Passkeys.SupportedAlgorithms = [edDsaAlgorithm],
                addDataProtection: false,
                configureServices: services => _ = services.AddSingleton<ISignatureVerifier, FakeEdDsaSignatureVerifier>());
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();

        var (userSubjectId, credentialId) = await CreateUserWithPasskeyUsingAlgorithmAsync(
            selfService, passkeyAuth, edDsaAlgorithm, serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(userSubjectId, _ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var fakeSignature = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: fakeSignature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success.UserSubjectId.ShouldBe(userSubjectId);
    }

    [Fact]
    public async Task BeginAsync_with_user_with_multiple_Passkeys_includes_all_in_AllowCredentials()
    {
        var (userSubjectId, firstCredentialId, _) = await CreateUserWithPasskey();
        var (secondCredentialId, _) = await AddPasskeyToUser(userSubjectId);

        var result = await _ceremonies.BeginAuthenticationAsync(userSubjectId, _ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        success.Session.Options.AllowCredentials.Count.ShouldBe(2);
        var firstCredentialIdBase64Url = Base64Url.EncodeToString(firstCredentialId);
        var secondCredentialIdBase64Url = Base64Url.EncodeToString(secondCredentialId);
        success.Session.Options.AllowCredentials.ShouldContain(c => c.Id == firstCredentialIdBase64Url);
        success.Session.Options.AllowCredentials.ShouldContain(c => c.Id == secondCredentialIdBase64Url);
    }

    [Fact]
    public async Task CompleteAsync_with_multiple_Passkeys_can_authenticate_with_either()
    {
        var (userSubjectId, firstCredentialId, firstEcdsa) = await CreateUserWithPasskey();
        var (secondCredentialId, secondEcdsa) = await AddPasskeyToUser(userSubjectId);

        // Authenticate with first passkey
        var beginResult1 = await _ceremonies.BeginAuthenticationAsync(userSubjectId, _ct);
        var beginSuccess1 = beginResult1.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData1 = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess1.Session.Options.Challenge,
            "https://example.com");
        var authenticatorData1 = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes1 = WebAuthnFixtures.DecodeBase64Url(clientData1);
        var signature1 = WebAuthnFixtures.CreateValidSignature(firstEcdsa, authenticatorData1, clientDataBytes1);

        var request1 = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess1.Session.ChallengeId,
            credentialId: firstCredentialId,
            clientDataJson: clientData1,
            authenticatorData: authenticatorData1,
            signature: signature1);

        // Act / Assert first passkey
        var result1 = await _ceremonies.CompleteAuthenticationAsync(request1, _ct);
        var success1 = result1.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success1.UserSubjectId.ShouldBe(userSubjectId);

        // Authenticate with second passkey
        var beginResult2 = await _ceremonies.BeginAuthenticationAsync(userSubjectId, _ct);
        var beginSuccess2 = beginResult2.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData2 = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess2.Session.Options.Challenge,
            "https://example.com");
        var authenticatorData2 = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes2 = WebAuthnFixtures.DecodeBase64Url(clientData2);
        var signature2 = WebAuthnFixtures.CreateValidSignature(secondEcdsa, authenticatorData2, clientDataBytes2);

        var request2 = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess2.Session.ChallengeId,
            credentialId: secondCredentialId,
            clientDataJson: clientData2,
            authenticatorData: authenticatorData2,
            signature: signature2);

        // Act / Assert second passkey
        var result2 = await _ceremonies.CompleteAuthenticationAsync(request2, _ct);
        var success2 = result2.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success2.UserSubjectId.ShouldBe(userSubjectId);
    }

    [Fact]
    public async Task CompleteAsync_with_user_verification_required_but_not_performed_returns_error()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.UserVerificationRequirement = PasskeyConstants.UserVerificationRequirement.Required);
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var (_, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge,
            "https://example.com");
        var authenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1); // UP only, no UV
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.UserVerificationRequired);
    }

    [Fact]
    public async Task CompleteAsync_with_user_verification_required_and_performed_returns_success()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.UserVerificationRequirement = PasskeyConstants.UserVerificationRequirement.Required);
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var (userSubjectId, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge,
            "https://example.com");
        var authenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x05, signCount: 1); // UP + UV
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success.UserSubjectId.ShouldBe(userSubjectId);
        success.SignCount.ShouldBe(1u);
        success.UserVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteAsync_with_backed_up_credential_returns_backed_up_true()
    {
        var (userSubjectId, credentialId, ecdsa) =
            await CreateUserWithPasskey(flags: 0x59); // UP + BE + BS + AT
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x19, signCount: 1); // UP + BE + BS
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success.UserSubjectId.ShouldBe(userSubjectId);
        success.BackedUp.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteAsync_with_changed_BackupEligibility_returns_error()
    {
        var (_, credentialId, ecdsa) = await CreateUserWithPasskey(flags: 0x49); // UP + BE + AT
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1); // UP only, BE mismatch
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.BackupEligibilityMismatch);
    }

    [Fact]
    public async Task CompleteAsync_with_backed_up_but_not_eligible_returns_error()
    {
        var (_, credentialId, ecdsa) = await CreateUserWithPasskey(); // default flags: UP + UV + AT (no BE)
        var beginResult = await _ceremonies.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x11,
                signCount: 1); // UP + BS (BS without BE)
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await _ceremonies.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.BackupEligibilityMismatch);
    }

    [Fact]
    public async Task Expired_authentication_challenge_is_rejected()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ChallengeTimeout = TimeSpan.FromSeconds(300));
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var (_, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        // Advance time beyond the configured timeout
        timeProvider.Advance(TimeSpan.FromSeconds(301));

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.ChallengeExpired);
    }

    [Fact]
    public async Task Non_expired_challenge_succeeds()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ChallengeTimeout = TimeSpan.FromSeconds(300));
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var (_, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(_ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        // Advance time to just under the configured timeout
        timeProvider.Advance(TimeSpan.FromSeconds(299));

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
    }

    private sealed class FakeEdDsaSignatureVerifier : ISignatureVerifier
    {
        public int Algorithm => -8; // EdDSA

        public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) => true;
    }

    [Fact]
    public async Task CompleteAsync_with_allowed_origins_matching_origin_succeeds()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com", "https://app.example.com"];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var (userSubjectId, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(userSubjectId, _ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://app.example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
    }

    [Fact]
    public async Task CompleteAsync_with_allowed_origins_non_matching_origin_returns_origin_mismatch()
    {
        //Arrange
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com"];
        });
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var (userSubjectId, credentialId, ecdsa) =
            await CreateUserWithPasskey(authenticatorsSelfService, passkeyAuth, externalAuthenticator: serviceProvider.GetRequiredService<IExternalAuthenticator>());

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(userSubjectId, _ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://evil.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        //Act
        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        //Assert
        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteAsync_with_empty_allowed_origins_returns_origin_mismatch()
    {
        //Arrange
        var createOptions = new Action<UserAuthenticationOptions>(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com"];
        });

        await using var setupServiceProvider =
            await UsersServiceProviderFactory.CreateWithOptionsAsync(createOptions);

        var setupPasskeyAuth = setupServiceProvider.GetRequiredService<IPasskeyCeremonies>();
        var setupAuthenticatorsSelfService = setupServiceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var (userSubjectId, credentialId, ecdsa) =
            await CreateUserWithPasskey(setupAuthenticatorsSelfService, setupPasskeyAuth, externalAuthenticator: setupServiceProvider.GetRequiredService<IExternalAuthenticator>());

        var authenticationOptions = new Action<UserAuthenticationOptions>(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = [];
        });

        await using var serviceProvider =
            await UsersServiceProviderFactory.CreateWithOptionsAsync(authenticationOptions);
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var authenticatorsSelfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _ = (await serviceProvider.GetRequiredService<IUserAuthenticatorsAdmin>()
            .TryAddAsync(userSubjectId, [], [TestData.CreateExternalAuthenticatorAddress()], _ct)).ShouldNotBeNull();

        var beginRegistration =
            await setupPasskeyAuth.BeginRegistrationAsync(userSubjectId, "user@example.com", "Test User", _ct);
        var registrationClientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create,
            beginRegistration.Options.Challenge, "https://example.com");
        var registrationAttestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            PasskeyConstants.AttestationFormat.None,
            "example.com",
            credentialId,
            ecdsa);
        var registrationRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            challengeId: beginRegistration.ChallengeId,
            clientDataJson: registrationClientData,
            attestationObject: registrationAttestationObject,
            credentialId: credentialId,
            name: "Test Passkey");
        var registrationResult = await setupPasskeyAuth.CompleteRegistrationAsync(registrationRequest, _ct);
        var registrationSuccess = registrationResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await authenticatorsSelfService.TryAddPasskeyAsync(userSubjectId, registrationSuccess.Credential, _ct))
            .ShouldBeTrue();

        var beginResult = await passkeyAuth.BeginAuthenticationAsync(userSubjectId, _ct);
        var beginSuccess = beginResult.ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get,
            beginSuccess.Session.Options.Challenge, "https://example.com");
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData("example.com", flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var request = WebAuthnFixtures.CreateCompleteAuthenticationRequest(
            challengeId: beginSuccess.Session.ChallengeId,
            credentialId: credentialId,
            clientDataJson: clientData,
            authenticatorData: authenticatorData,
            signature: signature);

        //Act
        var result = await passkeyAuth.CompleteAuthenticationAsync(request, _ct);

        //Assert
        var failure = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
        failure.Error.ShouldBe(AuthenticationCompleteError.OriginMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Origin validation failed because AllowedOrigins is not configured");
    }

    private async Task<(UserSubjectId UserSubjectId, byte[] CredentialId)>
        CreateUserWithPasskeyUsingAlgorithmAsync(
            IUserAuthenticatorsSelfService selfService,
            IPasskeyCeremonies ceremonies,
            int algorithm,
            IExternalAuthenticator? externalAuthenticator = null)
    {
        externalAuthenticator ??= _externalAuthenticator;
        // Create a user
        var subjectId = (await externalAuthenticator.TryAuthenticateAsync(TestData.CreateExternalAuthenticatorAddress(), _ct))
            .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        // Register a passkey with the specified algorithm
        var registrationSession =
            await ceremonies.BeginRegistrationAsync(subjectId, "user@example.com", "Test User", _ct);
        var credentialId = registrationSession.ChallengeId.ToByteArray();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create,
            registrationSession.Options.Challenge,
            "https://example.com");
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObjectWithAlgorithm(PasskeyConstants.AttestationFormat.None,
                "example.com", credentialId, algorithm);

        var registrationRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            challengeId: registrationSession.ChallengeId,
            clientDataJson: clientData,
            attestationObject: attestationObject,
            credentialId: credentialId,
            name: "Test Passkey");

        var registrationResult =
            await ceremonies.CompleteRegistrationAsync(registrationRequest, _ct);
        var success = registrationResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await selfService.TryAddPasskeyAsync(subjectId, success.Credential, _ct)).ShouldBeTrue();

        return (subjectId, credentialId);
    }

    private async Task<(UserSubjectId UserSubjectId, byte[] CredentialId, ECDsa PrivateKey)>
        CreateUserWithPasskey(
            IUserAuthenticatorsSelfService? authenticatorsSelfService = null,
            IPasskeyCeremonies? passkeyAuth = null,
            byte flags = 0x45, // UP + AT + UV
            IExternalAuthenticator? externalAuthenticator = null)
    {
        authenticatorsSelfService ??= _authenticatorsSelfService;
        passkeyAuth ??= _ceremonies;
        externalAuthenticator ??= _externalAuthenticator;

        // Create a user
        var subjectId =
            (await externalAuthenticator.TryAuthenticateAsync(TestData.CreateExternalAuthenticatorAddress(), _ct))
            .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        // Generate a real EC key pair
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Register a passkey for the user with the real public key
        var registrationSession =
            await passkeyAuth.BeginRegistrationAsync(subjectId, "user@example.com", "Test User", _ct);
        var credentialId = registrationSession.ChallengeId.ToByteArray();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create,
            registrationSession.Options.Challenge,
            "https://example.com");
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            PasskeyConstants.AttestationFormat.None, "example.com", credentialId, ecdsa, flags: flags);

        var registrationRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            challengeId: registrationSession.ChallengeId,
            clientDataJson: clientData,
            attestationObject: attestationObject,
            credentialId: credentialId,
            name: "Test Passkey");

        var registrationResult =
            await passkeyAuth.CompleteRegistrationAsync(registrationRequest, _ct);
        var success = registrationResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await authenticatorsSelfService.TryAddPasskeyAsync(subjectId, success.Credential, _ct))
            .ShouldBeTrue();

        return (subjectId, credentialId, ecdsa);
    }

    private async Task<(byte[] CredentialId, ECDsa PrivateKey)> AddPasskeyToUser(
        UserSubjectId userSubjectId,
        string name = "Additional Passkey")
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var session = await _ceremonies.BeginRegistrationAsync(userSubjectId, "user@example.com", "Test User", _ct);
        var credentialId = RandomNumberGenerator.GetBytes(32);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create,
            session.Options.Challenge, "https://example.com");
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObjectWithEcdsa(PasskeyConstants.AttestationFormat.None, "example.com",
                credentialId, ecdsa);

        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            challengeId: session.ChallengeId,
            clientDataJson: clientData,
            attestationObject: attestationObject,
            credentialId: credentialId,
            name: name);

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);
        var success = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await _authenticatorsSelfService.TryAddPasskeyAsync(userSubjectId, success.Credential, _ct)).ShouldBeTrue();

        return (credentialId, ecdsa);
    }
}
