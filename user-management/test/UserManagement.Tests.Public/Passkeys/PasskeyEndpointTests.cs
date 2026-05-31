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
using Microsoft.AspNetCore.Mvc;

namespace Duende.Platform.UserManagement.Passkeys;

public class PasskeyEndpointTests(WebServerFixture webServer) : IAsyncDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly UserAuthenticationFixture _fixture = new(webServer);

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Register_begin_returns401_when_unauthenticated()
    {
        await _fixture.InitializeAsync();

        var response = await _fixture.NonRedirectingClient.PostAsync("/passkeys/register/begin", null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_complete_returns401_when_unauthenticated()
    {
        await _fixture.InitializeAsync();

        var response = await _fixture.NonRedirectingClient.PostAsJsonAsync("/passkeys/register/complete",
            new { challengeId = Guid.NewGuid(), id = "x", rawId = "x", type = PasskeyConstants.CredentialType.PublicKey, response = new { } }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticate_discoverable_begin_returns_ok_with_ChallengeId_and_options()
    {
        await _fixture.InitializeAsync();

        var response = await _fixture.Client.PostAsync("/passkeys/authenticate/discoverable/begin", null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        json.GetProperty("challengeId").GetGuid().ShouldNotBe(Guid.Empty);
        json.GetProperty("options").GetRawText().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Full_registration_flow_returns_ok_with_CredentialId()
    {
        await _fixture.InitializeAsync();

        var (subjectId, _) = await _fixture.SeedAuthenticatorsAsync();

        await UserAuthenticationFixture.SignInClientAsync(_fixture.NonRedirectingClient, subjectId.ToString());

        var beginResponse = await _fixture.NonRedirectingClient.PostAsync("/passkeys/register/begin", null, _ct);
        beginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var beginJson = await beginResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var challengeId = beginJson.GetProperty("challengeId").GetGuid();
        var challenge = beginJson.GetProperty("options").GetProperty("challenge").GetString()!;

        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = challengeId.ToByteArray();
        var clientData =
            WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create, challenge, _fixture.Origin);
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObjectWithEcdsa(PasskeyConstants.AttestationFormat.None, _fixture.RelyingPartyId, credentialId, ecdsa,
                flags: 0x45);

        var completeBody = new
        {
            challengeId,
            id = Base64Url.EncodeToString(credentialId),
            rawId = Base64Url.EncodeToString(credentialId),
            type = PasskeyConstants.CredentialType.PublicKey,
            response = new
            {
                clientDataJSON = clientData,
                attestationObject = Base64Url.EncodeToString(attestationObject)
            },
            name = "Test Passkey"
        };

        var completeResponse =
            await _fixture.NonRedirectingClient.PostAsJsonAsync("/passkeys/register/complete", completeBody, _ct);
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var completeJson = await completeResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        completeJson.GetProperty("credentialId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Full_discoverable_authentication_flow_returns_ok_and_sets_cookie()
    {
        await _fixture.InitializeAsync();

        var (subjectId, _) = await _fixture.SeedAuthenticatorsAsync();
        var (credentialId, ecdsa) = await _fixture.SeedPasskeyAsync(subjectId, "Test Passkey");

        // Discoverable begin (no userName required)
        var beginResponse =
            await _fixture.NonRedirectingClient.PostAsync("/passkeys/authenticate/discoverable/begin", null, _ct);
        beginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var beginJson = await beginResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var challengeId = beginJson.GetProperty("challengeId").GetGuid();
        var challenge = beginJson.GetProperty("options").GetProperty("challenge").GetString()!;

        // Build assertion
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

        var completeResponse =
            await _fixture.NonRedirectingClient.PostAsJsonAsync("/passkeys/authenticate/complete", completeBody, _ct);
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completeJson = await completeResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var properties = completeJson.EnumerateObject().Select(static p => p.Name).ToList();
        properties.ShouldBe(["userVerified", "backedUp"], ignoreOrder: false,
            customMessage: "Response must contain only userVerified and backedUp.");
        completeResponse.Headers.Contains("Set-Cookie").ShouldBeTrue();
    }

    [Fact]
    public async Task Authenticate_complete_returns_generic_error_on_failure()
    {
        await _fixture.InitializeAsync();

        var completeBody = new
        {
            challengeId = Guid.CreateVersion7(),
            id = "dummy",
            rawId = "dummy",
            type = PasskeyConstants.CredentialType.PublicKey,
            response = new
            {
                clientDataJSON = "dummy",
                authenticatorData = "dummy",
                signature = "dummy"
            }
        };

        var response = await _fixture.NonRedirectingClient.PostAsJsonAsync(
            "/passkeys/authenticate/complete", completeBody, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Unable to authenticate with passkey.");
    }

    [Fact]
    public async Task Register_complete_returns_generic_error_on_failure()
    {
        await _fixture.InitializeAsync();

        var (subjectId, _) = await _fixture.SeedAuthenticatorsAsync();
        await UserAuthenticationFixture.SignInClientAsync(_fixture.NonRedirectingClient, subjectId.ToString());

        var completeBody = new
        {
            challengeId = Guid.CreateVersion7(),
            id = "dummy",
            rawId = "dummy",
            type = PasskeyConstants.CredentialType.PublicKey,
            response = new
            {
                clientDataJSON = "dummy",
                attestationObject = "dummy"
            },
            name = "Test Passkey"
        };

        var response = await _fixture.NonRedirectingClient.PostAsJsonAsync(
            "/passkeys/register/complete", completeBody, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Unable to complete passkey registration.");
    }

    [Fact]
    public async Task Can_customize_endpoint_routes()
    {
        _fixture.ConfigureBuilder = authentication =>
            authentication.ConfigureEndpoints(o => o.Passkeys.Route = "/my-passkeys");

        await _fixture.InitializeAsync();

        // Default path should 404
        var defaultResponse =
            await _fixture.NonRedirectingClient.PostAsync("/passkeys/authenticate/discoverable/begin", null, _ct);
        defaultResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Custom path should 200
        var customResponse =
            await _fixture.NonRedirectingClient.PostAsync("/my-passkeys/authenticate/discoverable/begin", null, _ct);
        customResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasskeysJs_returns_ok_with_java_script_content_type()
    {
        await _fixture.InitializeAsync();

        var response = await _fixture.Client.GetAsync("/passkeys/js", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/javascript");
    }

    [Fact]
    public async Task PasskeysJs_contains_configured_endpoint_paths()
    {
        await _fixture.InitializeAsync();

        var response = await _fixture.Client.GetAsync("/passkeys/js", _ct);
        var content = await response.Content.ReadAsStringAsync(_ct);

        // Should contain the default full paths
        ShouldlyExtensions.ShouldContain(content, "/passkeys/register/begin");
        ShouldlyExtensions.ShouldContain(content, "/passkeys/register/complete");
        ShouldlyExtensions.ShouldContain(content, "/passkeys/authenticate/begin");
        ShouldlyExtensions.ShouldContain(content, "/passkeys/authenticate/discoverable/begin");
        ShouldlyExtensions.ShouldContain(content, "/passkeys/authenticate/complete");

        // Should NOT contain any placeholder tokens
        content.ShouldNotContain("{registerBeginUrl}");
        content.ShouldNotContain("{registerCompleteUrl}");
        content.ShouldNotContain("{authenticateBeginUrl}");
        content.ShouldNotContain("{authenticateDiscoverableBeginUrl}");
        content.ShouldNotContain("{authenticateCompleteUrl}");
    }

    [Fact]
    public async Task PasskeysJs_reflects_custom_route_configuration()
    {
        _fixture.ConfigureBuilder = authentication =>
            authentication.ConfigureEndpoints(o => o.Passkeys.Route = "/my-passkeys");
        await _fixture.InitializeAsync();

        var response = await _fixture.Client.GetAsync("/my-passkeys/js", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(_ct);

        // Should contain the custom base route in the full paths
        ShouldlyExtensions.ShouldContain(content, "/my-passkeys/register/begin");
        ShouldlyExtensions.ShouldContain(content, "/my-passkeys/authenticate/complete");

        // Should NOT contain the default route
        content.ShouldNotContain("/passkeys/");
    }

}
