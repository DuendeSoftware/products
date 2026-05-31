// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Passkeys;

public class PasskeySignInHandlerTests(WebServerFixture webServer) : IAsyncDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly UserAuthenticationFixture _fixture = new(webServer);

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Custom_sign_in_handler_is_invoked_during_passkey_authentication()
    {
        var customHandler = new SpyPasskeySignInHandler();
        _fixture.ConfigureServices = services =>
        {
            _ = services.AddSingleton(customHandler);
            _ = services.AddScoped<IPasskeySignInHandler>(sp => sp.GetRequiredService<SpyPasskeySignInHandler>());
        };

        await _fixture.InitializeAsync();

        var (subjectId, _) = await _fixture.SeedAuthenticatorsAsync();
        var (credentialId, ecdsa) = await _fixture.SeedPasskeyAsync(subjectId, "Test Passkey");

        var response = await CompleteDiscoverableAuthenticationAsync(credentialId, ecdsa);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        customHandler.WasCalled.ShouldBeTrue();
        _ = customHandler.LastUser.ShouldNotBeNull();
        customHandler.LastUser.SubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Custom_sign_in_handler_can_return_custom_result()
    {
        var customHandler = new CustomResultPasskeySignInHandler();
        _fixture.ConfigureServices = services =>
        {
            _ = services.AddSingleton(customHandler);
            _ = services.AddScoped<IPasskeySignInHandler>(sp => sp.GetRequiredService<CustomResultPasskeySignInHandler>());
        };

        await _fixture.InitializeAsync();

        var (subjectId, _) = await _fixture.SeedAuthenticatorsAsync();
        var (credentialId, ecdsa) = await _fixture.SeedPasskeyAsync(subjectId, "Test Passkey");

        var response = await CompleteDiscoverableAuthenticationAsync(credentialId, ecdsa);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        json.GetProperty("custom").GetBoolean().ShouldBeTrue();
        json.GetProperty("subjectId").GetString().ShouldBe(subjectId.Value);
    }

    [Fact]
    public async Task Custom_sign_in_handler_receives_UserVerified_and_BackedUp()
    {
        var customHandler = new SpyPasskeySignInHandler();
        _fixture.ConfigureServices = services =>
        {
            _ = services.AddSingleton(customHandler);
            _ = services.AddScoped<IPasskeySignInHandler>(sp => sp.GetRequiredService<SpyPasskeySignInHandler>());
        };

        await _fixture.InitializeAsync();

        var (subjectId, _) = await _fixture.SeedAuthenticatorsAsync();
        var (credentialId, ecdsa) = await _fixture.SeedPasskeyAsync(subjectId, "Test Passkey");

        _ = await CompleteDiscoverableAuthenticationAsync(credentialId, ecdsa);

        customHandler.WasCalled.ShouldBeTrue();
        // The test passkey fixture uses flags 0x01 (user present but not verified, not backed up)
        customHandler.LastUserVerified.ShouldBeFalse();
        customHandler.LastBackedUp.ShouldBeFalse();
    }

    private async Task<HttpResponseMessage> CompleteDiscoverableAuthenticationAsync(byte[] credentialId, ECDsa ecdsa)
    {
        var beginResponse =
            await _fixture.NonRedirectingClient.PostAsync("/passkeys/authenticate/discoverable/begin", null, _ct);
        beginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var beginJson = await beginResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var challengeId = beginJson.GetProperty("challengeId").GetGuid();
        var challenge = beginJson.GetProperty("options").GetProperty("challenge").GetString()!;

        var clientData =
            WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Get, challenge, _fixture.Origin);
        var authenticatorData =
            WebAuthnFixtures.CreateAuthenticatorData(_fixture.RelyingPartyId, flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        var completeBody = new
        {
            challengeId,
            id = Base64Url.EncodeToString(credentialId),
            rawId = Base64Url.EncodeToString(credentialId),
            type = PasskeyConstants.CredentialType.PublicKey,
            response = new
            {
                clientDataJSON = clientData,
                authenticatorData = Base64Url.EncodeToString(authenticatorData),
                signature
            }
        };

        return await _fixture.NonRedirectingClient.PostAsJsonAsync("/passkeys/authenticate/complete", completeBody, _ct);
    }

    private sealed class SpyPasskeySignInHandler : IPasskeySignInHandler
    {
        public bool WasCalled { get; private set; }
        public UserAuthenticators? LastUser { get; private set; }
        public bool LastUserVerified { get; private set; }
        public bool LastBackedUp { get; private set; }

        public Task<IResult> SignInAsync(HttpContext context, UserAuthenticators user, bool userVerified, bool backedUp, Ct ct)
        {
            WasCalled = true;
            LastUser = user;
            LastUserVerified = userVerified;
            LastBackedUp = backedUp;
            return Task.FromResult<IResult>(new PasskeyCompleteAuthenticationResult(userVerified, backedUp));
        }
    }

    private sealed class CustomResultPasskeySignInHandler : IPasskeySignInHandler
    {
        public Task<IResult> SignInAsync(HttpContext context, UserAuthenticators user, bool userVerified, bool backedUp, Ct ct)
        {
            var result = Results.Json(new { custom = true, subjectId = user.SubjectId.Value });
            return Task.FromResult(result);
        }
    }
}
