// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using FluentAssertions;
using IntegrationTests.Common;
using Xunit;

namespace IntegrationTests.Endpoints.Authorize;

public sealed class PushedAuthorizationUnregisteredRedirectUriTests
{
    private const string ImplicitClientId = "implicit_client";
    private const string ImplicitAndClientCredentialsClientId = "implicit_and_client_credentials_client";
    private const string ClientSecret = "secret";
    private const string RegisteredRedirectUri = "https://implicit.example/callback";
    private const string UnregisteredRedirectUri = "https://unregistered.example/callback";
    private readonly IdentityServerPipeline _pipeline = new();

    public PushedAuthorizationUnregisteredRedirectUriTests()
    {
        _pipeline.Clients.AddRange(new[]
        {
            new Client
            {
                ClientId = ImplicitClientId,
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireClientSecret = true,
                ClientSecrets = { new Secret(ClientSecret.Sha256()) },
                RequireConsent = false,
                AllowedScopes = { "openid", "api" },
                RedirectUris = { RegisteredRedirectUri },
                AllowAccessTokensViaBrowser = true
            },
            new Client
            {
                ClientId = ImplicitAndClientCredentialsClientId,
                AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
                RequireClientSecret = true,
                ClientSecrets = { new Secret(ClientSecret.Sha256()) },
                RequireConsent = false,
                AllowedScopes = { "openid", "api" },
                RedirectUris = { "https://implicit-and-client-credentials.example/callback" },
                AllowAccessTokensViaBrowser = true
            }
        });
        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Claims = { new Claim("name", "Bob Loblaw") }
        });
        _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _pipeline.ApiScopes.Add(new ApiScope("api"));
        _pipeline.ApiResources.Add(new ApiResource("api") { Scopes = { "api" } });
        _pipeline.Initialize(enableLogging: true);
        _pipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = true;
        _pipeline.Options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
    }

    [Theory]
    [InlineData("id_token", "openid")]
    [InlineData("id_token token", "openid api")]
    public async Task implicit_only_client_without_credentials_should_reject_unregistered_redirect_uri(string responseType, string scope)
    {
        var (json, statusCode) = await PushAsync(ImplicitClientId, UnregisteredRedirectUri, responseType, scope);

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        json.Should().NotBeNull();
        json.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        json.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task implicit_only_client_with_valid_secret_should_reject_unregistered_redirect_uri()
    {
        var (json, statusCode) = await PushAsync(ImplicitClientId, UnregisteredRedirectUri, secret: ClientSecret);

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        json.Should().NotBeNull();
        json.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        json.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task implicit_only_client_with_bogus_secret_should_reject_unregistered_redirect_uri()
    {
        var (json, statusCode) = await PushAsync(ImplicitClientId, UnregisteredRedirectUri, secret: "bogus");

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        json.Should().NotBeNull();
        json.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        json.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task implicit_only_client_with_registered_redirect_uri_should_complete_authorize_flow()
    {
        var (json, statusCode) = await PushAsync(ImplicitClientId, RegisteredRedirectUri);
        statusCode.Should().Be(HttpStatusCode.Created);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();
        requestUri.Should().NotBeNullOrWhiteSpace();

        var response = await AuthorizeAsync(ImplicitClientId, requestUri);

        // releases/is/7.2.x redirects with 302 Found (main uses 303 See Other)
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.ToString().Should().StartWith(RegisteredRedirectUri);
        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.Should().BeFalse();
        authorization.IdentityToken.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task implicit_and_client_credentials_client_without_valid_credentials_should_reject_par(string secret)
    {
        var (json, statusCode) = await PushAsync(ImplicitAndClientCredentialsClientId, UnregisteredRedirectUri, secret: secret);

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        json.Should().NotBeNull();
        json.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        json.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.TokenErrors.InvalidClient);
    }

    [Fact]
    public async Task implicit_and_client_credentials_client_with_valid_credentials_should_complete_authorize_flow_for_unregistered_redirect_uri()
    {
        var (json, statusCode) = await PushAsync(ImplicitAndClientCredentialsClientId, UnregisteredRedirectUri, secret: ClientSecret);
        statusCode.Should().Be(HttpStatusCode.Created);
        var requestUri = json.RootElement.GetProperty("request_uri").GetString();
        requestUri.Should().NotBeNullOrWhiteSpace();

        var response = await AuthorizeAsync(ImplicitAndClientCredentialsClientId, requestUri);

        // releases/is/7.2.x redirects with 302 Found (main uses 303 See Other)
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.ToString().Should().StartWith(UnregisteredRedirectUri);
        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.Should().BeFalse();
        authorization.IdentityToken.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("fragment")]
    [InlineData("form_post")]
    public async Task preexisting_unregistered_redirect_uri_should_not_deliver_tokens_to_attacker(string responseMode)
    {
        var referenceValue = Guid.NewGuid().ToString("N");
        var pushedAuthorizationService = _pipeline.Resolve<IPushedAuthorizationService>();
        await pushedAuthorizationService.StoreAsync(new DeserializedPushedAuthorizationRequest
        {
            ReferenceValue = referenceValue,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            PushedParameters = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, ImplicitClientId },
                { OidcConstants.AuthorizeRequest.ResponseType, "id_token token" },
                { OidcConstants.AuthorizeRequest.ResponseMode, responseMode },
                { OidcConstants.AuthorizeRequest.Scope, "openid api" },
                { OidcConstants.AuthorizeRequest.RedirectUri, UnregisteredRedirectUri },
                { OidcConstants.AuthorizeRequest.Nonce, "nonce" },
                { OidcConstants.AuthorizeRequest.State, "state" }
            }
        });
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var requestUri = $"{IdentityServerConstants.PushedAuthorizationRequestUri}:{referenceValue}";
        var authorizeUrl = _pipeline.CreateAuthorizeUrl(clientId: ImplicitClientId, requestUri: requestUri);

        var response = await _pipeline.BrowserClient.GetAsync(authorizeUrl);
        var content = await response.Content.ReadAsStringAsync();
        var location = response.Headers.Location?.ToString() ?? string.Empty;

        location.Should().NotStartWith(UnregisteredRedirectUri);
        content.Should().NotContain(UnregisteredRedirectUri);
        content.Should().NotContain("id_token");
        content.Should().NotContain("access_token");
        // releases/is/7.2.x redirects with 302 Found (main uses 303 See Other)
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        location.Should().Contain("/error");

        await _pipeline.BrowserClient.GetAsync(response.Headers.Location);
        _pipeline.ErrorWasCalled.Should().BeTrue();
        _pipeline.ErrorMessage.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        _pipeline.ErrorMessage.RedirectUri.Should().BeNull();
    }

    private async Task<(JsonDocument Json, HttpStatusCode StatusCode)> PushAsync(
        string clientId,
        string redirectUri,
        string responseType = "id_token",
        string scope = "openid",
        string secret = null)
    {
        var parameters = new Dictionary<string, string>
        {
            { OidcConstants.AuthorizeRequest.ClientId, clientId },
            { OidcConstants.AuthorizeRequest.ResponseType, responseType },
            { OidcConstants.AuthorizeRequest.Scope, scope },
            { OidcConstants.AuthorizeRequest.RedirectUri, redirectUri },
            { OidcConstants.AuthorizeRequest.Nonce, "nonce" },
            { OidcConstants.AuthorizeRequest.State, "state" }
        };
        if (secret != null)
        {
            parameters.Add(OidcConstants.TokenRequest.ClientSecret, secret);
        }

        return await _pipeline.PushAuthorizationRequestAsync(parameters);
    }

    private async Task<HttpResponseMessage> AuthorizeAsync(string clientId, string requestUri)
    {
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeUrl = _pipeline.CreateAuthorizeUrl(clientId: clientId, requestUri: requestUri);
        return await _pipeline.BrowserClient.GetAsync(authorizeUrl);
    }
}
