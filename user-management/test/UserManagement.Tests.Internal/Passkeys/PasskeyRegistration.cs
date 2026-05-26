// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using Duende.Platform.UserManagement.Passkeys.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class PasskeyRegistration : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasskeyCeremonies _ceremonies = null!;
    private WebAuthnRegistrationCeremony _webAuthnRegistrationCeremony = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IUserSelfService _userSelfService = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _ceremonies = _serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        _webAuthnRegistrationCeremony = _serviceProvider.GetRequiredService<WebAuthnRegistrationCeremony>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Challenge_should_be_unique()
    {
        var userSubjectId = UserSubjectId.New();

        var result1 = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);
        var result2 = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        result1.Options.Challenge.ShouldNotBe(result2.Options.Challenge);
    }

    [Fact]
    public async Task Challenge_should_be_cryptographically_random()
    {
        const int minimumChallengeBytes = 32;
        const int sampleSize = 1000;
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // Verify minimum length (32 bytes per WebAuthn recommendation)
        var challengeBytes = WebAuthnFixtures.DecodeBase64Url(result.Options.Challenge);
        challengeBytes.Length.ShouldBeGreaterThanOrEqualTo(minimumChallengeBytes);

        // Statistical test: generate multiple challenges and verify entropy
        var challenges = new HashSet<string>();
        for (var i = 0; i < sampleSize; i++)
        {
            var session = await _ceremonies.BeginRegistrationAsync(
                userSubjectId, "user@example.com", "Test User", _ct);
            _ = challenges.Add(session.Options.Challenge);
        }

        // All challenges should be unique (no collisions in N samples)
        challenges.Count.ShouldBe(sampleSize);

        // Verify byte distribution across samples (basic entropy check)
        var allBytes = challenges
            .SelectMany(WebAuthnFixtures.DecodeBase64Url)
            .ToArray();

        // Each byte value (0-255) should appear with reasonable frequency
        // In 1000 samples of 32 bytes = 32000 bytes, each value should appear ~125 times on average
        // We check that we see at least 200 distinct byte values (out of 256) to confirm good distribution
        var distinctByteValues = allBytes.Distinct().Count();
        distinctByteValues.ShouldBeGreaterThan(200);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public async Task Challenge_size_should_be_configurable(int challengeSize)
    {
        await using var serviceProvider =
            await UsersServiceProviderFactory.CreateWithOptionsAsync(options => options.Passkeys.ChallengeSize = challengeSize);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var challengeBytes = WebAuthnFixtures.DecodeBase64Url(result.Options.Challenge);
        challengeBytes.Length.ShouldBe(challengeSize);
    }

    [Fact]
    public async Task Session_should_be_unique()
    {
        var userSubjectId = UserSubjectId.New();

        var result1 = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);
        var result2 = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        result1.ChallengeId.ShouldNotBe(result2.ChallengeId);
    }

    [Fact]
    public async Task Should_support_required_algorithms()
    {
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // WebAuthn requires support for ES256 (-7) and RS256 (-257)
        result.Options.PubKeyCredParams.ShouldContain(p => p.Alg == -7, "Should support ES256 (alg: -7)");
        result.Options.PubKeyCredParams.ShouldContain(p => p.Alg == -257, "Should support RS256 (alg: -257)");
    }

    [Fact]
    public async Task User_id_should_be_opaque_handle()
    {
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // User ID should be base64url-encoded (opaque, not raw PII)
        result.Options.User.Id.ShouldNotBeNullOrWhiteSpace();
        var userIdBytes = WebAuthnFixtures.DecodeBase64Url(result.Options.User.Id);
        userIdBytes.Length.ShouldBeGreaterThan(0);

        // User ID should decode to the SHA-256 hash of the user subject ID
        var expectedBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userSubjectId.Value));
        userIdBytes.ShouldBe(expectedBytes);
    }

    [Fact]
    public async Task Challenge_should_be_Base64Url_encoded()
    {
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // Challenge should be valid base64url
        result.Options.Challenge.ShouldNotBeNullOrWhiteSpace();
        var challengeBytes = WebAuthnFixtures.DecodeBase64Url(result.Options.Challenge);
        challengeBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_ChallengeId_returns_challenge_not_found()
    {
        var request = new PasskeyCompleteRegistrationRequest
        {
            ChallengeId = Guid.CreateVersion7(),
            Id = "test-id",
            RawId = Base64Url.EncodeToString([1, 2, 3, 4]),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAttestationResponse
            {
                ClientDataJSON = Base64Url.EncodeToString("{}"u8),
                AttestationObject = Base64Url.EncodeToString([0xA0])
            },
            Name = "Test Passkey"
        };

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.ChallengeNotFound);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_challenge_mismatch_returns_challenge_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, "wrong-challenge", "https://example.com");
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, "https://example.com", "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.ChallengeMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_origin_mismatch_returns_origin_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://evil.com");
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, "https://example.com", "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_type_returns_invalid_type()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get, session.Options.Challenge, "https://example.com");
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, "https://example.com", "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidType);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_unsupported_format_returns_unsupported_AttestationFormat()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObject("fido-u2f", "example.com", credentialId); // "fido-u2f" is not a supported format
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.UnsupportedAttestationFormat);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_RpId_mismatch_returns_RpId_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // Create attestation with wrong RP ID (wrong.com instead of example.com)
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "wrong.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.RpIdMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_user_present_flag_not_set_returns_user_not_present()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // Create attestation with AT flag (0x40) but without UP flag (0x01)
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x40);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.UserNotPresent);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_unsupported_algorithm_returns_unsupported_algorithm()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // Create attestation with EdDSA algorithm (-8) which is not supported
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, algorithm: -8);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.UnsupportedAlgorithm);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_missing_attested_credential_returns_invalid_AttestationObject()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // Create attestation without credential data
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, includeCredentialData: false);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidAttestationObject);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_cbor_returns_invalid_AttestationObject()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // Create invalid CBOR data
        var invalidCbor = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, invalidCbor, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidAttestationObject);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_Base64Url_attestation_returns_invalid_AttestationObject()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var request = new PasskeyCompleteRegistrationRequest
        {
            ChallengeId = session.ChallengeId,
            Id = "test-id",
            RawId = Base64Url.EncodeToString([1, 2, 3, 4]),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAttestationResponse
            {
                ClientDataJSON = clientData,
                AttestationObject = "not-valid-base64!@#$%", // Invalid Base64Url
            },
            Name = "Test Passkey"
        };

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidAttestationObject);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_Base64Url_ClientData_returns_invalid_ClientData()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = new PasskeyCompleteRegistrationRequest
        {
            ChallengeId = session.ChallengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAttestationResponse
            {
                ClientDataJSON = "not-valid-base64!@#$%", // Invalid Base64Url
                AttestationObject = Base64Url.EncodeToString(attestationObject)
            },
            Name = "Test Passkey"
        };

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidClientData);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_malformed_ClientDataJson_returns_invalid_ClientData()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = new PasskeyCompleteRegistrationRequest
        {
            ChallengeId = session.ChallengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAttestationResponse
            {
                ClientDataJSON = Base64Url.EncodeToString("{ invalid json"u8), // Valid Base64Url, invalid JSON
                AttestationObject = Base64Url.EncodeToString(attestationObject)
            },
            Name = "Test Passkey"
        };

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidClientData);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_CredentialType_returns_invalid_CredentialType()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);

        var request = new PasskeyCompleteRegistrationRequest
        {
            ChallengeId = session.ChallengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = "invalid-type",
            Response = new AuthenticatorAttestationResponse
            {
                ClientDataJSON = clientData,
                AttestationObject = Base64Url.EncodeToString(attestationObject)
            },
            Name = "Test Passkey"
        };

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidCredentialType);
    }

    [Fact]
    public async Task Challenge_is_deleted_after_successful_registration()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var successResult = await _ceremonies.CompleteRegistrationAsync(request, _ct);
        _ = successResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();

        // Assert: Verify challenge was deleted by attempting to reuse it
        var failedResult = await _ceremonies.CompleteRegistrationAsync(request, _ct);
        var failure = failedResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.ChallengeNotFound);
    }

    [Fact]
    public async Task Challenge_is_deleted_after_failed_registration()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // Create a request with mismatched origin (will fail)
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://evil.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var failureResult = await _ceremonies.CompleteRegistrationAsync(request, _ct);
        var failure = failureResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);

        // Assert: Verify challenge was deleted: a second attempt should return ChallengeNotFound
        var correctClientData =
            WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var correctRequest =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, correctClientData, attestationObject, credentialId, "Test Passkey");

        var secondResult = await _ceremonies.CompleteRegistrationAsync(correctRequest, _ct);
        var secondFailure = secondResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        secondFailure.Error.ShouldBe(RegistrationError.ChallengeNotFound);
    }

    [Fact]
    public async Task AuthenticatorSelection_should_include_default_values()
    {
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.AuthenticatorSelection.ShouldNotBeNull();
        result.Options.AuthenticatorSelection.AuthenticatorAttachment.ShouldBeNull(); // Default: null (any)
        result.Options.AuthenticatorSelection.UserVerification.ShouldBe(PasskeyConstants.UserVerificationRequirement.Preferred); // Default: preferred
    }

    [Theory]
    [InlineData("platform")]
    [InlineData("cross-platform")]
    public async Task Authenticator_attachment_should_be_configurable(string authenticatorAttachment)
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.AuthenticatorAttachment = authenticatorAttachment);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.AuthenticatorSelection.ShouldNotBeNull();
        result.Options.AuthenticatorSelection.AuthenticatorAttachment.ShouldBe(authenticatorAttachment);
    }

    [Theory]
    [InlineData("required")]
    [InlineData("preferred")]
    [InlineData("discouraged")]
    public async Task User_verification_requirement_should_be_mapped_to_AuthenticatorSelection(string userVerification)
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.UserVerificationRequirement = userVerification);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.AuthenticatorSelection.ShouldNotBeNull();
        result.Options.AuthenticatorSelection.UserVerification.ShouldBe(userVerification);
    }

    [Fact]
    public async Task ResidentKey_requirement_should_default_to_preferred()
    {
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.AuthenticatorSelection.ShouldNotBeNull();
        result.Options.AuthenticatorSelection.ResidentKey.ShouldBe(PasskeyConstants.ResidentKeyRequirement.Preferred);
    }

    [Theory]
    [InlineData(PasskeyConstants.ResidentKeyRequirement.Discouraged)]
    [InlineData(PasskeyConstants.ResidentKeyRequirement.Preferred)]
    [InlineData(PasskeyConstants.ResidentKeyRequirement.Required)]
    public async Task Resident_key_requirement_should_be_configurable(string residentKey)
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ResidentKeyRequirement = residentKey);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.AuthenticatorSelection.ShouldNotBeNull();
        result.Options.AuthenticatorSelection.ResidentKey.ShouldBe(residentKey);
    }

    [Fact]
    public async Task ResidentKey_should_be_mapped_to_AuthenticatorSelection()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ResidentKeyRequirement = PasskeyConstants.ResidentKeyRequirement.Required);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.AuthenticatorSelection.ShouldNotBeNull();
        result.Options.AuthenticatorSelection.ResidentKey.ShouldBe(PasskeyConstants.ResidentKeyRequirement.Required);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_cross_origin_request_returns_origin_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData =
            WebAuthnFixtures.CreateClientDataJsonWithCrossOrigin(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://evil.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_empty_origin_returns_origin_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_invalid_uri_origin_returns_origin_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "not-a-valid-uri");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_scheme_mismatch_returns_origin_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // Client sends http, expected is https
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "http://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_port_mismatch_returns_origin_mismatch()
    {
        var userSubjectId = UserSubjectId.New();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // Client sends :8080, expected is default port
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com:8080");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_explicit_default_port_succeeds()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        // Client sends :443 explicitly, expected is https://example.com (implicit 443)
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com:443");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        // Assert: This should succeed as :443 is the default HTTPS port
        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task When_ServerDomain_null_uses_origin_as_RelyingPartyId()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = null);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Uses_ServerDomain_as_origin_as_RelyingPartyId_when_configured()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = "example.com");

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Fails_when_received_subdomain_does_not_match_configured_ServerDomain()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = "example.com");

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();

        var userSubjectId = UserSubjectId.New();
        var session = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://app1.example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task BeginAsync_sets_RelyingPartyId_when_ServerDomain_configured()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ServerDomain = "example.com");

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var session = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        session.Options.RelyingParty.Id.ShouldBe("example.com");
    }

    [Fact]
    public async Task Supported_algorithms_default_returns_all_registered_algorithms()
    {
        var userSubjectId = UserSubjectId.New();

        var result = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        result.Options.PubKeyCredParams.ShouldContain(p => p.Alg == CoseAlgorithms.Es256);
        result.Options.PubKeyCredParams.ShouldContain(p => p.Alg == CoseAlgorithms.Rs256);
    }

    [Fact]
    public async Task Supported_algorithms_can_be_restricted()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Es256]);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.PubKeyCredParams.ShouldHaveSingleItem();
        result.Options.PubKeyCredParams[0].Alg.ShouldBe(CoseAlgorithms.Es256);
    }

    [Fact]
    public async Task Supported_algorithms_respects_preference_order()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Rs256, CoseAlgorithms.Es256]);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        result.Options.PubKeyCredParams.Count.ShouldBe(2);
        result.Options.PubKeyCredParams[0].Alg.ShouldBe(CoseAlgorithms.Rs256);
        result.Options.PubKeyCredParams[1].Alg.ShouldBe(CoseAlgorithms.Es256);
    }

    [Fact]
    public async Task Supported_algorithms_filters_out_unregistered_verifiers()
    {
        const int edDsaAlgorithm = -8; // EdDSA - not registered by default

        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Es256, edDsaAlgorithm]);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();

        var result = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        _ = result.Options.PubKeyCredParams.ShouldHaveSingleItem();
        result.Options.PubKeyCredParams[0].Alg.ShouldBe(CoseAlgorithms.Es256);
    }

    [Fact]
    public void Get_pub_key_cred_params_returns_all_algorithms_in_registered_preference_order()
    {
        var result = _webAuthnRegistrationCeremony.GetPubKeyCredParams();

        result.Select(x => x.Alg).ShouldBe([
            CoseAlgorithms.Es256,
            CoseAlgorithms.Es384,
            CoseAlgorithms.Es512,
            CoseAlgorithms.Rs256,
            CoseAlgorithms.Rs384,
            CoseAlgorithms.Rs512,
            CoseAlgorithms.Ps256,
            CoseAlgorithms.Ps384,
            CoseAlgorithms.Ps512,
            CoseAlgorithms.Rs1
        ]);
    }

    [Fact]
    public async Task Get_pub_key_cred_params_filters_to_supported_algorithms()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Es256, CoseAlgorithms.Rs256]);
        var ceremony = serviceProvider.GetRequiredService<WebAuthnRegistrationCeremony>();

        var result = ceremony.GetPubKeyCredParams();

        result.Select(x => x.Alg).ShouldBe([CoseAlgorithms.Es256, CoseAlgorithms.Rs256]);
    }

    [Fact]
    public async Task CompleteAsync_rejects_algorithm_not_in_supported_algorithms()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Es256]);

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();
        var session = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // Create attestation with RS256 algorithm (-257) which is NOT in SupportedAlgorithms
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, algorithm: CoseAlgorithms.Rs256);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.UnsupportedAlgorithm);
    }

    [Fact]
    public async Task Second_Passkey_registration_adds_second_credential_to_user()
    {
        var (userSubjectId, userName) = await CreateUserAsync();

        // Register first passkey
        var firstSession = await _ceremonies.BeginRegistrationAsync(userSubjectId, userName.ToString(), "Test User", _ct);
        var firstCredentialId = RandomNumberGenerator.GetBytes(32);
        var firstClientData =
            WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, firstSession.Options.Challenge, "https://example.com");
        var firstAttestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", firstCredentialId);
        var firstRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(firstSession.ChallengeId, firstClientData,
            firstAttestationObject, firstCredentialId, "First Passkey");

        var firstResult = await _ceremonies.CompleteRegistrationAsync(firstRequest, _ct);
        var firstSuccess = firstResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await _authenticatorsSelfService.TryAddPasskeyAsync(userSubjectId, firstSuccess.Credential, _ct)).ShouldBeTrue();

        // Register second passkey
        var secondSession = await _ceremonies.BeginRegistrationAsync(userSubjectId, userName.ToString(), "Test User", _ct);
        var secondCredentialId = RandomNumberGenerator.GetBytes(32);
        var secondClientData =
            WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, secondSession.Options.Challenge, "https://example.com");
        var secondAttestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", secondCredentialId);
        var secondRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(secondSession.ChallengeId, secondClientData,
            secondAttestationObject, secondCredentialId, "Second Passkey");

        var secondResult = await _ceremonies.CompleteRegistrationAsync(secondRequest, _ct);
        var secondSuccess = secondResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await _authenticatorsSelfService.TryAddPasskeyAsync(userSubjectId, secondSuccess.Credential, _ct)).ShouldBeTrue();

        // Act / Assert: Verify both passkeys were persisted on the user.
        var authenticators = (await _authenticatorsSelfService.TryGetAsync(userSubjectId, _ct)).ShouldNotBeNull();
        authenticators.Passkeys.Count.ShouldBe(2);
        authenticators.Passkeys.ShouldContain(x => x.CredentialId == PasskeyCredentialId.From(firstCredentialId));
        authenticators.Passkeys.ShouldContain(x => x.CredentialId == PasskeyCredentialId.From(secondCredentialId));
    }

    [Fact]
    public async Task BeginAsync_excludes_existing_credentials()
    {
        var (userSubjectId, userName) = await CreateUserAsync();

        // Register first passkey
        var firstSession = await _ceremonies.BeginRegistrationAsync(userSubjectId, userName.ToString(), "Test User", _ct);
        var firstCredentialId = RandomNumberGenerator.GetBytes(32);
        var firstClientData =
            WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, firstSession.Options.Challenge, "https://example.com");
        var firstAttestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", firstCredentialId);
        var firstRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(firstSession.ChallengeId, firstClientData,
            firstAttestationObject, firstCredentialId, "Test Passkey");

        var firstResult = await _ceremonies.CompleteRegistrationAsync(firstRequest, _ct);
        var firstSuccess = firstResult.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await _authenticatorsSelfService.TryAddPasskeyAsync(userSubjectId, firstSuccess.Credential, _ct)).ShouldBeTrue();

        var secondSession = await _ceremonies.BeginRegistrationAsync(userSubjectId, userName.ToString(), "Test User", _ct);

        // Assert: Verify excludeCredentials contains the first passkey's credential ID
        _ = secondSession.Options.ExcludeCredentials.ShouldHaveSingleItem();
        var expectedCredentialIdBase64Url = Base64Url.EncodeToString(firstCredentialId);
        secondSession.Options.ExcludeCredentials[0].Id.ShouldBe(expectedCredentialIdBase64Url);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_user_verification_required_but_not_performed_returns_error()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.UserVerificationRequirement = PasskeyConstants.UserVerificationRequirement.Required);
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();
        var session = await passkeyAuth.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // flags: 0x41 = UP (0x01) + AT (0x40), no UV (0x04)
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x41);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.UserVerificationRequired);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_user_verification_required_and_performed_returns_success()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.UserVerificationRequirement = PasskeyConstants.UserVerificationRequirement.Required);
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        // flags: 0x45 = UP (0x01) + UV (0x04) + AT (0x40)
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x45);
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Expired_registration_challenge_is_rejected()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ChallengeTimeout = TimeSpan.FromSeconds(300));

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();

        var externalAuthenticator = TestData.CreateExternalAuthenticator();
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), externalAuthenticator, _ct)).ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        // Advance time beyond the configured timeout
        timeProvider.Advance(TimeSpan.FromSeconds(301));

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.ChallengeExpired);
    }

    [Fact]
    public async Task Non_expired_challenge_succeeds()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ChallengeTimeout = TimeSpan.FromSeconds(300));

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();

        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        // Advance time to just under the configured timeout
        timeProvider.Advance(TimeSpan.FromSeconds(299));

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Custom_challenge_timeout_is_respected()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passkeys.ChallengeTimeout = TimeSpan.FromSeconds(60));

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();

        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        // Advance time beyond the custom timeout
        timeProvider.Advance(TimeSpan.FromSeconds(61));

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.ChallengeExpired);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_backup_eligible_flag_sets_backup_eligible()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x49); // UP + BE + AT
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var success = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        success.Credential.BackupEligible.ShouldBeTrue();
        success.Credential.BackedUp.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_backed_up_credential_sets_both_flags()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x59); // UP + BE + BS + AT
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var success = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        success.Credential.BackupEligible.ShouldBeTrue();
        success.Credential.BackedUp.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteRegistrationAsync_without_backup_flags_defaults_to_false()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x41); // UP + AT
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var success = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        success.Credential.BackupEligible.ShouldBeFalse();
        success.Credential.BackedUp.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_backed_up_but_not_eligible_returns_failure()
    {
        var (userSubjectId, _) = await CreateUserAsync();
        var session = await _ceremonies.BeginRegistrationAsync(
            userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId, flags: 0x51); // UP + BS + AT (BS without BE)
        var request =
            WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await _ceremonies.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.InvalidBackupState);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_allowed_origins_matching_origin_succeeds()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com", "https://app.example.com"];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://app.example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_allowed_origins_non_matching_origin_returns_origin_mismatch()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com"];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var userSubjectId = UserSubjectId.New();
        var session = await passkeyAuth.BeginRegistrationAsync(userSubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://evil.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_empty_allowed_origins_returns_origin_mismatch()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.AllowedOrigins = [];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var session = await passkeyAuth.BeginRegistrationAsync(UserSubjectId.New(), "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "Origin validation failed because AllowedOrigins is not configured");
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_non_matching_http_ip_AllowedOrigins_returns_origin_mismatch()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["http://192.168.1.10"];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var session = await passkeyAuth.BeginRegistrationAsync(UserSubjectId.New(), "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_AllowedOrigins_entry_containing_path_returns_origin_mismatch()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com/path"];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var session = await passkeyAuth.BeginRegistrationAsync(UserSubjectId.New(), "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.OriginMismatch);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "valid entry");
    }

    [Fact]
    public async Task CompleteRegistrationAsync_with_multi_origin_AllowedOrigins_accepts_any_listed_origin()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com", "https://app.example.com", "https://auth.example.com"];
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://auth.example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(PasskeyConstants.AttestationFormat.None, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    private async Task<(UserSubjectId UserSubjectId, UserName UserName)> CreateUserAsync()
    {
        var userName = TestData.CreateUserName();
        var authenticators = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        (await _userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, _ct)).ShouldBeTrue();
        return (authenticators.SubjectId, userName);
    }
}
