// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Duende.IdentityServer.Models.JsonWebKey;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

public sealed class PushedAuthorizationRedirectUriSchemeTests
{
    private const string ClientId = "par-code-client";
    private const string ClientSecret = "secret";
    private const string CodeVerifier = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerPipeline _pipeline = new();
    private readonly RecordingPushedAuthorizationService _pushedAuthorizationService = new();

    public PushedAuthorizationRedirectUriSchemeTests()
    {
        var rsaKey = CryptoHelper.CreateRsaSecurityKey();
        var privateKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        _pipeline.ClientKeys.Add(ClientId, privateKey);
        _pipeline.Clients.Add(new Client
        {
            ClientId = ClientId,
            ClientSecrets =
            {
                new Secret(ClientSecret.Sha256()),
                new Secret(System.Text.Json.JsonSerializer.Serialize(new JsonWebKey
                {
                    kty = "RSA",
                    use = "sig",
                    kid = rsaKey.KeyId,
                    e = privateKey.E,
                    n = privateKey.N,
                    alg = privateKey.Alg
                }))
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey
                }
            },
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = { "https://registered.example/callback" },
            RequireConsent = false,
            RequirePkce = true,
            AllowedScopes = { IdentityServerConstants.StandardScopes.OpenId }
        });
        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob"
        });
        _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _pipeline.OnPostConfigureServices += services =>
        {
            services.RemoveAll<IPushedAuthorizationService>();
            services.AddTransient<IPushedAuthorizationService>(provider =>
            {
                _pushedAuthorizationService.Inner = new PushedAuthorizationService(
                    provider.GetRequiredService<IPushedAuthorizationSerializer>(),
                    provider.GetRequiredService<IPushedAuthorizationRequestStore>());
                return _pushedAuthorizationService;
            });
        };
        _pipeline.Initialize();
        _pipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = true;
        _pipeline.Options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        _ = _pipeline.ApplicationServices.GetRequiredService<IPushedAuthorizationService>();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    public async Task disallowed_scheme_should_be_rejected_without_persistence(string redirectUri)
    {
        var (json, statusCode) = await PushAsync(redirectUri);

        var error = json.RootElement.GetProperty("error").GetString();
        var hasRequestUri = json.RootElement.TryGetProperty("request_uri", out _);
        var storeCount = _pushedAuthorizationService.StoreCount;
        statusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        hasRequestUri.ShouldBeFalse();
        storeCount.ShouldBe(0);
    }

    [Fact]
    public async Task unregistered_https_uri_should_complete_code_flow_and_be_persisted()
    {
        const string redirectUri = "https://unregistered.example/callback";

        var (json, statusCode) = await PushAsync(redirectUri);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();
        var referenceValue = GetReferenceValue(requestUri);
        var stored = await _pushedAuthorizationService.GetPushedAuthorizationRequestAsync(referenceValue, _ct);
        var storeCount = _pushedAuthorizationService.StoreCount;
        var storedRedirectUri = stored?.PushedParameters[OidcConstants.AuthorizeRequest.RedirectUri];

        statusCode.ShouldBe(HttpStatusCode.Created);
        storeCount.ShouldBe(1);
        stored.ShouldNotBeNull();
        storedRedirectUri.ShouldBe(redirectUri);

        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeResponse = await _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl(
            clientId: ClientId,
            requestUri: requestUri), _ct);
        var authorizeRedirectUri = authorizeResponse.Headers.Location;
        var authorization = new AuthorizeResponse(authorizeRedirectUri.ToString());

        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        authorizeRedirectUri.ShouldNotBeNull();
        authorizeRedirectUri.AbsoluteUri.ShouldStartWith(redirectUri);
        authorization.IsError.ShouldBeFalse();
        authorization.Code.ShouldNotBeNull();

        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Code = authorization.Code,
            RedirectUri = redirectUri,
            CodeVerifier = CodeVerifier
        }, _ct);
        tokenResponse.IsError.ShouldBeFalse();
        tokenResponse.AccessToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task jar_effective_https_redirect_uri_should_complete_code_flow()
    {
        const string redirectUri = "https://jar.example/callback";

        var (json, statusCode) = await PushJarAsync(redirectUri);

        var requestUri = json.RootElement.GetProperty("request_uri").GetString();
        statusCode.ShouldBe(HttpStatusCode.Created);
        requestUri.ShouldNotBeNull();

        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeResponse = await _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl(
            clientId: ClientId,
            requestUri: requestUri), _ct);
        var authorizeRedirectUri = authorizeResponse.Headers.Location;

        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        authorizeRedirectUri.ShouldNotBeNull();
        authorizeRedirectUri.AbsoluteUri.ShouldStartWith(redirectUri);

        var authorization = new AuthorizeResponse(authorizeRedirectUri.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.Code.ShouldNotBeNull();

        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Code = authorization.Code,
            RedirectUri = redirectUri,
            CodeVerifier = CodeVerifier
        }, _ct);
        tokenResponse.IsError.ShouldBeFalse();
        tokenResponse.AccessToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task jar_effective_javascript_redirect_uri_should_be_rejected()
    {
        const string redirectUri = "javascript:alert(1)";

        var (json, statusCode) = await PushJarAsync(redirectUri);

        var error = json.RootElement.GetProperty("error").GetString();
        statusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task preexisting_unsafe_request_should_be_rejected_safely_at_authorize_endpoint()
    {
        var referenceValue = Guid.NewGuid().ToString("N");
        await _pushedAuthorizationService.StoreAsync(CreateStoredRequest(referenceValue, "javascript:alert(1)"), _ct);

        await AssertSafeAuthorizeRejectionAsync(referenceValue, "javascript:alert(1)");
    }

    [Fact]
    public async Task accepted_request_should_be_rechecked_against_current_policy()
    {
        var (json, statusCode) = await PushAsync("https://unregistered.example/callback");
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();

        statusCode.ShouldBe(HttpStatusCode.Created);

        _pipeline.Options.Validation.InvalidRedirectUriPrefixes.Add("https://unregistered.example");

        await AssertSafeAuthorizeRejectionAsync(GetReferenceValue(requestUri), "https://unregistered.example/callback");
    }

    [Fact]
    public async Task jar_accepted_request_should_be_rechecked_against_current_policy()
    {
        const string redirectUri = "https://jar.example/callback";
        var (json, statusCode) = await PushJarAsync(redirectUri);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();

        statusCode.ShouldBe(HttpStatusCode.Created);

        _pipeline.Options.Validation.InvalidRedirectUriPrefixes.Add("https://jar.example");

        await AssertSafeAuthorizeRejectionAsync(GetReferenceValue(requestUri), redirectUri);
    }

    [Fact]
    public async Task jar_unregistered_redirect_uri_should_be_rejected_when_unregistered_uris_are_disabled()
    {
        const string redirectUri = "https://jar.example/callback";
        var (json, statusCode) = await PushJarAsync(redirectUri);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();

        statusCode.ShouldBe(HttpStatusCode.Created);

        _pipeline.Options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = false;

        await AssertSafeAuthorizeRejectionAsync(GetReferenceValue(requestUri), redirectUri);
    }

    private async Task<(System.Text.Json.JsonDocument Json, HttpStatusCode StatusCode)> PushJarAsync(string redirectUri) =>
        await _pipeline.PushAuthorizationRequestUsingJarAsync(
            clientId: ClientId,
            clientSecret: ClientSecret,
            responseType: OidcConstants.ResponseTypes.Code,
            scope: IdentityServerConstants.StandardScopes.OpenId,
            redirectUri: redirectUri,
            extraJwt: new Dictionary<string, string>
            {
                [OidcConstants.AuthorizeRequest.CodeChallenge] = CreateCodeChallenge(),
                [OidcConstants.AuthorizeRequest.CodeChallengeMethod] = OidcConstants.CodeChallengeMethods.Sha256
            });

    private async Task<(System.Text.Json.JsonDocument Json, HttpStatusCode StatusCode)> PushAsync(string redirectUri) =>
        await _pipeline.PushAuthorizationRequestAsync(
            clientId: ClientId,
            clientSecret: ClientSecret,
            responseType: OidcConstants.ResponseTypes.Code,
            scope: IdentityServerConstants.StandardScopes.OpenId,
            redirectUri: redirectUri,
            nonce: null,
            extra: new Dictionary<string, string>
            {
                [OidcConstants.AuthorizeRequest.CodeChallenge] = CreateCodeChallenge(),
                [OidcConstants.AuthorizeRequest.CodeChallengeMethod] = OidcConstants.CodeChallengeMethods.Sha256
            });

    private async Task AssertSafeAuthorizeRejectionAsync(string referenceValue, string rejectedRedirectUri)
    {
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl(
            clientId: ClientId,
            requestUri: $"{IdentityServerConstants.PushedAuthorizationRequestUri}:{referenceValue}"), _ct);
        var location = response.Headers.Location;
        var locationText = location?.ToString();

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        location.ShouldNotBeNull();
        locationText.ShouldContain("/error");
        locationText.ShouldNotContain(rejectedRedirectUri);
        locationText.ShouldNotContain("code=");
        locationText.ShouldNotContain("access_token");

        var body = await response.Content.ReadAsStringAsync();

        body.ShouldNotContain(rejectedRedirectUri);
        body.ShouldNotContain("code=");
        body.ShouldNotContain("access_token");

        await _pipeline.BrowserClient.GetAsync(location, _ct);
        var error = _pipeline.ErrorMessage.Error;
        var errorRedirectUri = _pipeline.ErrorMessage.RedirectUri;

        error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        errorRedirectUri.ShouldBeNull();
    }

    private static DeserializedPushedAuthorizationRequest CreateStoredRequest(string referenceValue, string redirectUri) => new()
    {
        ReferenceValue = referenceValue,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        PushedParameters = new NameValueCollection
        {
            [OidcConstants.AuthorizeRequest.ClientId] = ClientId,
            [OidcConstants.AuthorizeRequest.ResponseType] = OidcConstants.ResponseTypes.Code,
            [OidcConstants.AuthorizeRequest.Scope] = IdentityServerConstants.StandardScopes.OpenId,
            [OidcConstants.AuthorizeRequest.RedirectUri] = redirectUri,
            [OidcConstants.AuthorizeRequest.CodeChallenge] = CreateCodeChallenge(),
            [OidcConstants.AuthorizeRequest.CodeChallengeMethod] = OidcConstants.CodeChallengeMethods.Sha256
        }
    };

    private static string CreateCodeChallenge() => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(CodeVerifier)));

    private static string GetReferenceValue(string requestUri) =>
        requestUri[(IdentityServerConstants.PushedAuthorizationRequestUri.Length + 1)..];

    private sealed class RecordingPushedAuthorizationService : IPushedAuthorizationService
    {
        public IPushedAuthorizationService Inner { private get; set; }

        public int StoreCount { get; private set; }

        public Task ConsumeAsync(string referenceValue, Ct ct) => Inner.ConsumeAsync(referenceValue, ct);

        public Task<DeserializedPushedAuthorizationRequest> GetPushedAuthorizationRequestAsync(string referenceValue, Ct ct) =>
            Inner.GetPushedAuthorizationRequestAsync(referenceValue, ct);

        public async Task StoreAsync(DeserializedPushedAuthorizationRequest pushedAuthorizationRequest, Ct ct)
        {
            StoreCount++;
            await Inner.StoreAsync(pushedAuthorizationRequest, ct);
        }
    }
}
