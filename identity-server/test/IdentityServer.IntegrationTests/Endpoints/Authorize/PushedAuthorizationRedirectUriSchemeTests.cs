// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using FluentAssertions;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using JsonWebKey = Duende.IdentityServer.Models.JsonWebKey;
using WilsonJsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace IntegrationTests.Endpoints.Authorize;

public sealed class PushedAuthorizationRedirectUriSchemeTests
{
    private const string ClientId = "par-code-client";
    private const string ClientSecret = "secret";
    private const string CodeVerifier = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly IdentityServerPipeline _pipeline = new();
    private readonly RecordingPushedAuthorizationService _pushedAuthorizationService = new();
    private readonly WilsonJsonWebKey _privateKey;

    public PushedAuthorizationRedirectUriSchemeTests()
    {
        var rsaKey = CryptoHelper.CreateRsaSecurityKey();
        _privateKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
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
                    e = _privateKey.E,
                    n = _privateKey.N,
                    alg = _privateKey.Alg
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
        statusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        hasRequestUri.Should().BeFalse();
        storeCount.Should().Be(0);
    }

    [Fact]
    public async Task unregistered_https_uri_should_complete_code_flow_and_be_persisted()
    {
        const string redirectUri = "https://unregistered.example/callback";

        var (json, statusCode) = await PushAsync(redirectUri);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();
        var referenceValue = GetReferenceValue(requestUri);
        var stored = await _pushedAuthorizationService.GetPushedAuthorizationRequestAsync(referenceValue);
        var storeCount = _pushedAuthorizationService.StoreCount;
        var storedRedirectUri = stored?.PushedParameters[OidcConstants.AuthorizeRequest.RedirectUri];

        statusCode.Should().Be(HttpStatusCode.Created);
        storeCount.Should().Be(1);
        stored.Should().NotBeNull();
        storedRedirectUri.Should().Be(redirectUri);

        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeResponse = await _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl(
            clientId: ClientId,
            requestUri: requestUri));
        var authorizeRedirectUri = authorizeResponse.Headers.Location;

        // releases/is/7.2.x redirects with 302 Found (main uses 303 See Other)
        authorizeResponse.StatusCode.Should().Be(HttpStatusCode.Found);
        authorizeRedirectUri.Should().NotBeNull();
        authorizeRedirectUri.AbsoluteUri.Should().StartWith(redirectUri);
        var authorization = new AuthorizeResponse(authorizeRedirectUri.ToString());
        authorization.IsError.Should().BeFalse();
        authorization.Code.Should().NotBeNull();

        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Code = authorization.Code,
            RedirectUri = redirectUri,
            CodeVerifier = CodeVerifier
        });
        tokenResponse.IsError.Should().BeFalse();
        tokenResponse.AccessToken.Should().NotBeNull();
    }

    [Fact]
    public async Task jar_effective_https_redirect_uri_should_complete_code_flow()
    {
        const string redirectUri = "https://jar.example/callback";

        var (json, statusCode) = await PushJarAsync(redirectUri);

        var requestUri = json.RootElement.GetProperty("request_uri").GetString();
        statusCode.Should().Be(HttpStatusCode.Created);
        requestUri.Should().NotBeNull();

        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeResponse = await _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl(
            clientId: ClientId,
            requestUri: requestUri));
        var authorizeRedirectUri = authorizeResponse.Headers.Location;

        // releases/is/7.2.x redirects with 302 Found (main uses 303 See Other)
        authorizeResponse.StatusCode.Should().Be(HttpStatusCode.Found);
        authorizeRedirectUri.Should().NotBeNull();
        authorizeRedirectUri.AbsoluteUri.Should().StartWith(redirectUri);

        var authorization = new AuthorizeResponse(authorizeRedirectUri.ToString());
        authorization.IsError.Should().BeFalse();
        authorization.Code.Should().NotBeNull();

        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Code = authorization.Code,
            RedirectUri = redirectUri,
            CodeVerifier = CodeVerifier
        });
        tokenResponse.IsError.Should().BeFalse();
        tokenResponse.AccessToken.Should().NotBeNull();
    }

    [Fact]
    public async Task jar_effective_javascript_redirect_uri_should_be_rejected()
    {
        const string redirectUri = "javascript:alert(1)";

        var (json, statusCode) = await PushJarAsync(redirectUri);

        var error = json.RootElement.GetProperty("error").GetString();
        statusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task preexisting_unsafe_request_should_be_rejected_safely_at_authorize_endpoint()
    {
        var referenceValue = Guid.NewGuid().ToString("N");
        await _pushedAuthorizationService.StoreAsync(CreateStoredRequest(referenceValue, "javascript:alert(1)"));

        await AssertSafeAuthorizeRejectionAsync(referenceValue, "javascript:alert(1)");
    }

    [Fact]
    public async Task accepted_request_should_be_rechecked_against_current_policy()
    {
        var (json, statusCode) = await PushAsync("https://unregistered.example/callback");
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();

        statusCode.Should().Be(HttpStatusCode.Created);

        _pipeline.Options.Validation.InvalidRedirectUriPrefixes.Add("https://unregistered.example");

        await AssertSafeAuthorizeRejectionAsync(GetReferenceValue(requestUri), "https://unregistered.example/callback");
    }

    [Fact]
    public async Task jar_accepted_request_should_be_rechecked_against_current_policy()
    {
        const string redirectUri = "https://jar.example/callback";
        var (json, statusCode) = await PushJarAsync(redirectUri);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();

        statusCode.Should().Be(HttpStatusCode.Created);

        _pipeline.Options.Validation.InvalidRedirectUriPrefixes.Add("https://jar.example");

        await AssertSafeAuthorizeRejectionAsync(GetReferenceValue(requestUri), redirectUri);
    }

    [Fact]
    public async Task jar_unregistered_redirect_uri_should_be_rejected_when_unregistered_uris_are_disabled()
    {
        const string redirectUri = "https://jar.example/callback";
        var (json, statusCode) = await PushJarAsync(redirectUri);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();

        statusCode.Should().Be(HttpStatusCode.Created);

        _pipeline.Options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = false;

        await AssertSafeAuthorizeRejectionAsync(GetReferenceValue(requestUri), redirectUri);
    }

    /// <summary>
    /// Pushes a JAR-wrapped code request. releases/is/7.2.x has no
    /// IdentityServerPipeline.PushAuthorizationRequestUsingJarAsync / ClientKeys
    /// helpers, so the request object is built and signed here with the same
    /// claims those helpers use on main.
    /// </summary>
    private async Task<(System.Text.Json.JsonDocument Json, HttpStatusCode StatusCode)> PushJarAsync(string redirectUri)
    {
        var jwtPayload = new Dictionary<string, string>
        {
            { OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code },
            { OidcConstants.AuthorizeRequest.ClientId, ClientId },
            { OidcConstants.AuthorizeRequest.RedirectUri, redirectUri },
            { OidcConstants.AuthorizeRequest.Scope, IdentityServerConstants.StandardScopes.OpenId },
            { OidcConstants.AuthorizeRequest.State, "123_state" },
            { OidcConstants.AuthorizeRequest.Nonce, "123_nonce" },
            { OidcConstants.AuthorizeRequest.CodeChallenge, CreateCodeChallenge() },
            { OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Sha256 }
        };

        var jwt = new JwtSecurityToken(
            new JwtHeader(new SigningCredentials(_privateKey, SecurityAlgorithms.RsaSha256)),
            new JwtPayload(ClientId, IdentityServerPipeline.BaseUrl,
                jwtPayload.Select(x => new Claim(x.Key, x.Value)),
                notBefore: null,
                expires: DateTime.UtcNow.AddMinutes(10)));
        var jar = new JwtSecurityTokenHandler().WriteToken(jwt);

        return await _pipeline.PushAuthorizationRequestAsync(new Dictionary<string, string>
        {
            { OidcConstants.AuthorizeRequest.ClientId, ClientId },
            { OidcConstants.TokenRequest.ClientSecret, ClientSecret },
            { OidcConstants.AuthorizeRequest.Request, jar }
        });
    }

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
            requestUri: $"{IdentityServerConstants.PushedAuthorizationRequestUri}:{referenceValue}"));
        var location = response.Headers.Location;
        var locationText = location?.ToString();

        // releases/is/7.2.x redirects with 302 Found (main uses 303 See Other)
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        location.Should().NotBeNull();
        locationText.Should().Contain("/error");
        locationText.Should().NotContain(rejectedRedirectUri);
        locationText.Should().NotContain("code=");
        locationText.Should().NotContain("access_token");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(rejectedRedirectUri);
        body.Should().NotContain("code=");
        body.Should().NotContain("access_token");

        await _pipeline.BrowserClient.GetAsync(location);
        var error = _pipeline.ErrorMessage.Error;
        var errorRedirectUri = _pipeline.ErrorMessage.RedirectUri;

        error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        errorRedirectUri.Should().BeNull();
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

        public Task ConsumeAsync(string referenceValue) => Inner.ConsumeAsync(referenceValue);

        public Task<DeserializedPushedAuthorizationRequest> GetPushedAuthorizationRequestAsync(string referenceValue) =>
            Inner.GetPushedAuthorizationRequestAsync(referenceValue);

        public async Task StoreAsync(DeserializedPushedAuthorizationRequest pushedAuthorizationRequest)
        {
            StoreCount++;
            await Inner.StoreAsync(pushedAuthorizationRequest);
        }
    }
}
