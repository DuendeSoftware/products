// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Mvc;

namespace Duende.Platform.UserManagement.Passkeys;

public class PasskeySecondFactorEndpointTests(WebServerFixture webServerFixture) : IAsyncDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly UserAuthenticationFixture _fixture = new(webServerFixture);

    private void ConfigureSecondFactorResolver(Func<UserSubjectId?> getSubjectId) =>
        _fixture.ConfigureBuilder = auth =>
                auth.EnablePasskeyForSecondFactor(new TestSecondFactorResolver(getSubjectId));

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Authenticate_begin_returns_not_found_when_second_factor_not_enabled()
    {
        await _fixture.InitializeAsync();

        var response = await _fixture.Client.PostAsync("/passkeys/authenticate/begin", null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Authenticate_begin_returns_bad_request_when_resolver_returns_no_subject_id()
    {
        ConfigureSecondFactorResolver(() => null);

        await _fixture.InitializeAsync();

        var response = await _fixture.NonRedirectingClient.PostAsync(
            "/passkeys/authenticate/begin", null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        _ = problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("Unable to resolve user for second-factor passkey authentication.");
    }

    [Fact]
    public async Task Authenticate_begin_returns_bad_request_when_resolved_user_has_no_passkeys()
    {
        ConfigureSecondFactorResolver(() => UserSubjectId.New());

        await _fixture.InitializeAsync();

        var response = await _fixture.NonRedirectingClient.PostAsync(
            "/passkeys/authenticate/begin", null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        _ = problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("Unable to authenticate with passkey.");
    }

    [Fact]
    public async Task Authenticate_begin_returns_ok_with_resolved_allowCredentials()
    {
        UserSubjectId? resolvedSubjectId = null;
        ConfigureSecondFactorResolver(() => resolvedSubjectId);

        await _fixture.InitializeAsync();

        var (subjectId, userName, _) = await _fixture.SeedAuthenticatorsAsync();
        var (firstCredentialId, _) = await _fixture.SeedPasskeyAsync(subjectId, userName, "Test Passkey 1");
        var (secondCredentialId, _) = await _fixture.SeedPasskeyAsync(subjectId, userName, "Test Passkey 2");

        resolvedSubjectId = subjectId;

        var response = await _fixture.NonRedirectingClient.PostAsync(
            "/passkeys/authenticate/begin", null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        json.GetProperty("challengeId").GetGuid().ShouldNotBe(Guid.Empty);

        var allowCredentials = json.GetProperty("options").GetProperty("allowCredentials");
        allowCredentials.GetArrayLength().ShouldBe(2);

        var credentialIds = allowCredentials
            .EnumerateArray()
            .Select(credential => credential.GetProperty("id").GetString())
            .ToArray();

        credentialIds.ShouldContain(Base64Url.EncodeToString(firstCredentialId));
        credentialIds.ShouldContain(Base64Url.EncodeToString(secondCredentialId));
    }

    [Fact]
    public async Task Authenticate_authentication_flow_succeeds_for_resolved_user()
    {
        UserSubjectId? resolvedSubjectId = null;
        ConfigureSecondFactorResolver(() => resolvedSubjectId);
        await _fixture.InitializeAsync();

        var (subjectId, userName, _) = await _fixture.SeedAuthenticatorsAsync();
        var (credentialId, ecdsa) = await _fixture.SeedPasskeyAsync(subjectId, userName, "Test Passkey");

        resolvedSubjectId = subjectId;

        var beginAuthenticationResponse = await _fixture.NonRedirectingClient.PostAsync(
            "/passkeys/authenticate/begin", null, _ct);
        beginAuthenticationResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var beginAuthenticationJson = await beginAuthenticationResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var challengeId = beginAuthenticationJson.GetProperty("challengeId").GetGuid();
        var challenge = beginAuthenticationJson.GetProperty("options").GetProperty("challenge").GetString()!;

        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Get, challenge, _fixture.Origin);

        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData(
            _fixture.RelyingPartyId, flags: 0x01, signCount: 1);

        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

        challengeId.ShouldNotBe(Guid.Empty);
        challenge.ShouldNotBeNullOrWhiteSpace();

        var completeAuthenticationBody = new
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

        var completeResponse = await _fixture.NonRedirectingClient.PostAsJsonAsync(
            "/passkeys/authenticate/complete", completeAuthenticationBody, _ct);

        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completeJson = await completeResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        completeJson.GetProperty("userVerified").GetBoolean().ShouldBeFalse();
        completeJson.GetProperty("backedUp").GetBoolean().ShouldBeFalse();
        completeResponse.Headers.Contains("Set-Cookie").ShouldBeTrue();
    }
}

internal sealed class TestSecondFactorResolver(Func<UserSubjectId?> getSubjectId)
    : ISecondFactorPasskeyAuthenticationResolver
{
    public Task<UserSubjectId?> ResolveAsync(CancellationToken ct) => Task.FromResult(getSubjectId());
}
