// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class EnablePasskeysHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasskeyCeremonies _ceremonies = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _ceremonies = _serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<PasskeyCredentialData?> RegisterPasskeyAsync(
        IPasskeyCeremonies ceremonies,
        IUserAuthenticatorsSelfService selfService,
        UserSubjectId subjectId,
        byte[] credentialId,
        ECDsa authenticatorKey,
        string origin,
        Ct ct)
    {
        // begin-snippet: passkeys-ceremonies-register
        var session = await ceremonies.BeginRegistrationAsync(subjectId, "user@example.com", "Example User", ct);

        // The browser's WebAuthn API builds this from the authenticator's response; WebAuthnFixtures
        // builds an equivalent response here, standing in for a real authenticator.
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create, session.Options.Challenge, origin);
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            PasskeyConstants.AttestationFormat.None, new Uri(origin).Host, credentialId, authenticatorKey, flags: 0x45);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId, clientData, attestationObject, credentialId, "My Passkey");

        var result = await ceremonies.CompleteRegistrationAsync(request, ct);
        if (result is not PasskeyRegistrationCompleteResult.Success success)
        {
            return null;
        }

        var saved = await selfService.TryAddPasskeyAsync(subjectId, success.Credential, ct);
        return saved ? success.Credential : null;
        // end-snippet
    }

    private static async Task<PasskeyAuthenticationCompleteResult> AuthenticateWithPasskeyAsync(
        IPasskeyCeremonies ceremonies,
        byte[] credentialId,
        ECDsa authenticatorKey,
        string origin,
        Ct ct)
    {
        // begin-snippet: passkeys-ceremonies-authenticate
        var beginResult = await ceremonies.BeginAuthenticationAsync(ct);
        var session = ((PasskeyAuthenticationBeginResult.Success)beginResult).Session;

        // The browser's WebAuthn API builds this from the authenticator's response; WebAuthnFixtures
        // builds an equivalent response here, standing in for a real authenticator.
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Get, session.Options.Challenge, origin);
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData(new Uri(origin).Host, flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(authenticatorKey, authenticatorData, clientDataBytes);
        var request = new PasskeyCompleteAuthenticationRequest
        {
            ChallengeId = session.ChallengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAssertionResponse
            {
                ClientDataJSON = clientData,
                AuthenticatorData = Base64Url.EncodeToString(authenticatorData),
                Signature = signature
            }
        };

        var result = await ceremonies.CompleteAuthenticationAsync(request, ct);
        // end-snippet

        return result;
    }

    private static async Task<bool> RemovePasskeyAsync(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, PasskeyCredentialId credentialId, Ct ct)
    {
        // begin-snippet: passkeys-remove
        var removed = await selfService.TryRemovePasskeyAsync(subjectId, credentialId, ct);
        // end-snippet
        return removed;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Can_register_a_passkey()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(32);

        var credential = await RegisterPasskeyAsync(_ceremonies, _selfService, subjectId, credentialId, key, "https://example.com", _ct);

        _ = credential.ShouldNotBeNull();
        var record = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        record.Passkeys.Select(p => p.CredentialId).ShouldContain(credential.CredentialId);
    }

    [Fact]
    public async Task Can_authenticate_with_a_registered_passkey()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        _ = (await RegisterPasskeyAsync(_ceremonies, _selfService, subjectId, credentialId, key, "https://example.com", _ct))
            .ShouldNotBeNull();

        var result = await AuthenticateWithPasskeyAsync(_ceremonies, credentialId, key, "https://example.com", _ct);

        var success = result.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success.UserSubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Can_remove_a_registered_passkey()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var credential = (await RegisterPasskeyAsync(_ceremonies, _selfService, subjectId, credentialId, key, "https://example.com", _ct))
            .ShouldNotBeNull();

        var removed = await RemovePasskeyAsync(_selfService, subjectId, credential.CredentialId, _ct);

        removed.ShouldBeTrue();
        var record = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        record.Passkeys.Select(p => p.CredentialId).ShouldNotContain(credential.CredentialId);

        var authResult = await AuthenticateWithPasskeyAsync(_ceremonies, credentialId, key, "https://example.com", _ct);
        _ = authResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();
    }

    [Fact]
    public async Task Registering_a_passkey_for_a_subject_with_no_authenticator_record_reports_failure()
    {
        // No IExternalAuthenticator.CreateUserAsync call for this subject, so it has no
        // UserAuthenticators record for TryAddPasskeyAsync to find, and the save step fails.
        var subjectId = UserSubjectId.New();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(32);

        var credential = await RegisterPasskeyAsync(_ceremonies, _selfService, subjectId, credentialId, key, "https://example.com", _ct);

        credential.ShouldBeNull();
    }
}
