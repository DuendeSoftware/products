// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using FluentAssertions;
using IdentityModel;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using JsonWebKey = Duende.IdentityServer.Models.JsonWebKey;
using WilsonJsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace IntegrationTests.Endpoints.Authorize;


public class PushedAuthorizationTests
{
    private readonly IdentityServerPipeline _mockPipeline = new();
    private Client _client;
    private string clientSecret = Guid.NewGuid().ToString();
    private Client _client2;
    private Client _publicClient;
    private const string PublicClientId = "par_public_client";
    private Client _privateKeyJwtClient;
    private const string PrivateKeyJwtClientId = "par_private_key_jwt_client";

    private WilsonJsonWebKey _privateKey;
    private JsonWebKey _publicKey;
    private WilsonJsonWebKey _privateKeyJwtSigningKey;
    private JsonWebKey _privateKeyJwtPublicKey;
    
    public PushedAuthorizationTests()
    {
        ConfigureClientKeys();
        ConfigureClients();
        ConfigureUsers();
        ConfigureScopesAndResources();

        _mockPipeline.OnPostConfigureServices += services =>
            new IdentityServerBuilder(services).AddJwtBearerClientAuthentication();
        _mockPipeline.Initialize(enableLogging: true);

        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = true;
    }

    [Fact]
    public async Task happy_path()
    {
        // Login
        await _mockPipeline.LoginAsync("bob");
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        // Push Authorization
        var expectedCallback = _client.RedirectUris.First();
        var expectedState = "123_state";
        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(
            redirectUri: expectedCallback,
            state: expectedState
        );
        statusCode.Should().Be(HttpStatusCode.Created);

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            extra: new
            {
                request_uri = parJson.RootElement.GetProperty("request_uri").GetString()
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        response.Should().Be302Found();
        response.Should().HaveHeader("Location").And.Match($"{expectedCallback}*");

        var authorization = new IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.Should().BeFalse();
        authorization.IdentityToken.Should().NotBeNull();
        authorization.State.Should().Be(expectedState);
    }

    [Fact]
    public async Task sensitive_values_should_not_be_logged_on_bad_request_to_par_endpoint()
    {
        // Login
        await _mockPipeline.LoginAsync("bob");
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        // Push Authorization
        var expectedCallback = _client.RedirectUris.First();
        var expectedState = "123_state";
        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(
            clientSecret: clientSecret,
            redirectUri: "bogus", // <-- Intentionally wrong, to provoke logging an error with raw request
            state: expectedState
        );
       
        _mockPipeline.MockLogger.LogMessages.Should().ContainMatch("*\"client_secret\": \"***REDACTED***\"*");
        _mockPipeline.MockLogger.LogMessages.Should().NotContainMatch(clientSecret);
    }

    [Fact]
    public async Task using_pushed_authorization_when_it_is_globally_disabled_fails()
    {
        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = false;
        
        var (_, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(clientSecret: clientSecret);
        statusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task not_using_pushed_authorization_when_it_is_globally_required_fails()
    {
        _mockPipeline.Options.PushedAuthorization.Required = true;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            nonce: "123_nonce");
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        // We expect to be redirected to the error page, as this is an interactive
        // call to authorize
        response.Should().Be302Found();
        response.Should().HaveHeader("Location").And.Match("*/error*"); 
    }

    [Fact]
    public async Task not_using_pushed_authorization_when_it_is_required_for_client_fails()
    {
        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint.Should().BeTrue();
        _mockPipeline.Options.PushedAuthorization.Required.Should().BeFalse();
        _client.RequirePushedAuthorization = true;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            nonce: "123_nonce");
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        // We expect to be redirected to the error page, as this is an interactive
        // call to authorize
        response.Should().Be302Found();
        response.Should().HaveHeader("Location").And.Match("*/error*"); 
    }

    [Fact]
    public async Task existing_pushed_authorization_request_uris_become_invalid_when_par_is_disabled()
    {
        // PAR is enabled when we push authorization...
        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(clientSecret: clientSecret);
        statusCode.Should().Be(HttpStatusCode.Created);
        parJson.Should().NotBeNull();

        // ... But then is later disabled, and then we try to use the pushed request
        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = false;

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            extra: new
            {
                request_uri = parJson.RootElement.GetProperty("request_uri").GetString()
            });

        // We expect to be redirected to the error page, as this is an interactive
        // call to authorize. We don't want to follow redirects. Instead we'll just
        // check for a 302 to the error page
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        authorizeResponse.Should().Be302Found();
        authorizeResponse.Should().HaveHeader("Location").And.Match("*/error*");
    }

    [Fact]
    public async Task reusing_pushed_authorization_request_uris_fails()
    {
        // Login
        await _mockPipeline.LoginAsync("bob");

        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(clientSecret: clientSecret);;
        statusCode.Should().Be(HttpStatusCode.Created);
        parJson.Should().NotBeNull();

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            extra: new
            {
                request_uri = parJson.RootElement.GetProperty("request_uri").GetString()
            });

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var firstAuthorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);
        var secondAuthorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        secondAuthorizeResponse.Should().Be302Found();
        secondAuthorizeResponse.Should().HaveHeader("Location").And.Match("*/error*");
    }

    [Theory]
    [InlineData("urn:ietf:params:oauth:request_uri:foo")]
    [InlineData("https://requests.example.com/bar")]
    [InlineData("nonsense")]
    public async Task pushed_authorization_with_a_request_uri_fails(string requestUri)
    {
        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(
            extra: new Dictionary<string, string>
            {
                { "request_uri", requestUri }
            });
        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }


    [Theory]
    [InlineData("prompt", "login")]
    [InlineData("prompt", "select_account")]
    [InlineData("prompt", "create")]
    [InlineData("max_age", "0")]
    public async Task prompt_login_can_be_used_with_pushed_authorization(string parameterName, string parameterValue)
    {
        // Login before we start (we expect to still be prompted to login because of the prompt param)
        _mockPipeline.Options.UserInteraction.CreateAccountUrl = IdentityServerPipeline.CreateAccountPage;
        _mockPipeline.Options.UserInteraction.PromptValuesSupported.Add(OidcConstants.PromptModes.Create);
        await _mockPipeline.LoginAsync("bob");
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        // Push Authorization
        var expectedCallback = _client.RedirectUris.First();
        var expectedState = "123_state";
        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync(
            redirectUri: expectedCallback,
            state: expectedState,
            extra: new Dictionary<string, string>
            {
                { parameterName, parameterValue }
            }
        );
        statusCode.Should().Be(HttpStatusCode.Created);

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            extra: new
            {
                request_uri = parJson.RootElement.GetProperty("request_uri").GetString()
            });
        var authorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        // Verify that authorize redirects to login
        authorizeResponse.Should().Be302Found();
        var isPromptCreate = parameterName == "prompt" && parameterValue == "create";
        var expectedLocation = isPromptCreate ? IdentityServerPipeline.CreateAccountPage : IdentityServerPipeline.LoginPage;
        authorizeResponse.Headers.Location.ToString().ToLower().Should().Match($"{expectedLocation.ToLower()}*");

        // Verify that the UI prompts the user at this point
        var uiResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeResponse.Headers.Location);
        uiResponse.Should().Be200Ok();

        // Now login and return to the return url we were given
        var returnPath = isPromptCreate ? _mockPipeline.CreateAccountReturnUrl : _mockPipeline.LoginReturnUrl;
        var returnUrl = new Uri(new Uri(IdentityServerPipeline.BaseUrl), returnPath);
        await _mockPipeline.LoginAsync("bob");
        var authorizeCallbackResponse = await _mockPipeline.BrowserClient.GetAsync(returnUrl);
        
        // The authorize callback should continue back to the application (the prompt parameter is processed so we don't go back to the UI)
        authorizeCallbackResponse.Should().Be302Found();
        authorizeCallbackResponse.Headers.Location.Should().Be(expectedCallback);
    }

    [Fact]
    public async Task request_is_rejected_when_basic_authenticated_client_does_not_match_client_id()
    {
        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client1",
            basicClientSecret: clientSecret,
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task request_is_rejected_when_public_authenticated_client_does_not_match_client_id()
    {
        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: PublicClientId,
            basicClientSecret: "irrelevant-nonempty-value",
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task request_is_rejected_when_private_key_jwt_authenticated_client_does_not_match_client_id()
    {
        var (parJson, statusCode) = await PushWithPrivateKeyJwtAsync(
            authenticatedClientId: PrivateKeyJwtClientId,
            signingKey: _privateKeyJwtSigningKey,
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task request_succeeds_when_public_authenticated_client_matches_client_id()
    {
        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: PublicClientId,
            basicClientSecret: "irrelevant-nonempty-value",
            form: new Dictionary<string, string>
            {
                { "client_id", PublicClientId },
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _publicClient.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.Created);
        parJson.Should().NotBeNull();
        parJson.RootElement.GetProperty("request_uri").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task request_succeeds_when_private_key_jwt_authenticated_client_matches_client_id()
    {
        var (parJson, statusCode) = await PushWithPrivateKeyJwtAsync(
            authenticatedClientId: PrivateKeyJwtClientId,
            signingKey: _privateKeyJwtSigningKey,
            form: new Dictionary<string, string>
            {
                { "client_id", PrivateKeyJwtClientId },
                { "response_type", "code" },
                { "scope", "openid profile" },
                { "redirect_uri", _privateKeyJwtClient.RedirectUris.First() },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.Created);
        parJson.Should().NotBeNull();
        parJson.RootElement.GetProperty("request_uri").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task request_is_rejected_when_private_key_jwt_signature_is_invalid()
    {
        var (parJson, statusCode) = await PushWithPrivateKeyJwtAsync(
            authenticatedClientId: PrivateKeyJwtClientId,
            signingKey: _privateKey, // wrong key: not par_private_key_jwt_client's
            form: new Dictionary<string, string>
            {
                { "client_id", PrivateKeyJwtClientId },
                { "response_type", "code" },
                { "scope", "openid profile" },
                { "redirect_uri", _privateKeyJwtClient.RedirectUris.First() },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.TokenErrors.InvalidClient);
    }

    [Fact]
    public async Task request_without_client_id_is_rejected()
    {
        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client1",
            basicClientSecret: clientSecret,
            form: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _client.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
    }

    [Fact]
    public async Task request_with_empty_client_id_is_rejected()
    {
        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client1",
            basicClientSecret: clientSecret,
            form: new Dictionary<string, string>
            {
                { "client_id", "" },
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _client.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
    }

    [Fact]
    public async Task request_is_rejected_when_client_id_differs_only_by_case()
    {
        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client1",
            basicClientSecret: clientSecret,
            form: new Dictionary<string, string>
            {
                { "client_id", "Client1" },
                { "response_type", "id_token" },
                { "scope", "openid profile" },
                { "redirect_uri", _client.RedirectUris.First() },
                { "nonce", "123_nonce" },
                { "state", "123_state" },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task jar_request_succeeds_when_basic_authenticated_client_matches_client_id()
    {
        var jar = BuildJarToken(
            issuer: "client2",
            signingKey: _privateKey,
            claims: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "client_id", "client2" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
                { "nonce", "123_nonce" },
            });

        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client2",
            basicClientSecret: "secret",
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.Created);
        parJson.Should().NotBeNull();
        parJson.RootElement.GetProperty("request_uri").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task jar_request_succeeds_when_private_key_jwt_authenticated_client_matches_client_id()
    {
        // The client_assertion (authenticates the client) and the "request" JAR
        // (carries the client_id claim) are distinct JWTs, both signed here for
        // the same client.
        var jar = BuildJarToken(
            issuer: PrivateKeyJwtClientId,
            signingKey: _privateKeyJwtSigningKey,
            claims: new Dictionary<string, string>
            {
                { "response_type", "code" },
                { "client_id", PrivateKeyJwtClientId },
                { "redirect_uri", _privateKeyJwtClient.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
            });

        var (parJson, statusCode) = await PushWithPrivateKeyJwtAsync(
            authenticatedClientId: PrivateKeyJwtClientId,
            signingKey: _privateKeyJwtSigningKey,
            form: new Dictionary<string, string>
            {
                { "client_id", PrivateKeyJwtClientId },
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.Created);
        parJson.Should().NotBeNull();
        parJson.RootElement.GetProperty("request_uri").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task jar_request_is_rejected_when_basic_authenticated_client_does_not_match_client_id()
    {
        var jar = BuildJarToken(
            issuer: "client2",
            signingKey: _privateKey,
            claims: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "client_id", "client2" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
                { "nonce", "123_nonce" },
            });

        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client1",
            basicClientSecret: clientSecret,
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task jar_request_is_rejected_when_private_key_jwt_authenticated_client_does_not_match_client_id()
    {
        var jar = BuildJarToken(
            issuer: "client2",
            signingKey: _privateKey,
            claims: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "client_id", "client2" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
                { "nonce", "123_nonce" },
            });

        var (parJson, statusCode) = await PushWithPrivateKeyJwtAsync(
            authenticatedClientId: PrivateKeyJwtClientId,
            signingKey: _privateKeyJwtSigningKey,
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task jar_request_is_rejected_when_payload_client_id_does_not_match_form_client_id()
    {
        var jar = BuildJarToken(
            issuer: "client2",
            signingKey: _privateKey,
            claims: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "client_id", "client1" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
                { "nonce", "123_nonce" },
            });

        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client2",
            basicClientSecret: "secret",
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        parJson.RootElement.GetProperty("error_description").GetString()
            .Should().Be("Invalid JWT request");
    }

    [Fact]
    public async Task jar_request_is_rejected_when_signature_is_invalid()
    {
        var jar = BuildJarToken(
            issuer: "client2",
            signingKey: _privateKeyJwtSigningKey, // wrong key: not client2's
            claims: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "client_id", "client2" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
                { "nonce", "123_nonce" },
            });

        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client2",
            basicClientSecret: "secret",
            form: new Dictionary<string, string>
            {
                { "client_id", "client2" },
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
        parJson.RootElement.GetProperty("error").GetString()
            .Should().Be(OidcConstants.AuthorizeErrors.InvalidRequestObject);
        parJson.RootElement.GetProperty("error_description").GetString()
            .Should().Be("Invalid JWT request");
    }

    [Fact]
    public async Task jar_request_without_form_client_id_is_rejected()
    {
        var jar = BuildJarToken(
            issuer: "client2",
            signingKey: _privateKey,
            claims: new Dictionary<string, string>
            {
                { "response_type", "id_token" },
                { "client_id", "client2" },
                { "redirect_uri", _client2.RedirectUris.First() },
                { "scope", "openid profile" },
                { "state", "123_state" },
                { "nonce", "123_nonce" },
            });

        var (parJson, statusCode) = await PushWithBasicAuthAsync(
            basicClientId: "client2",
            basicClientSecret: "secret",
            form: new Dictionary<string, string>
            {
                { "request", jar },
            });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        parJson.Should().NotBeNull();
        parJson.RootElement.TryGetProperty("request_uri", out _).Should().BeFalse();
    }

    private static string BuildJarToken(
        string issuer,
        WilsonJsonWebKey signingKey,
        Dictionary<string, string> claims,
        DateTime? expires = null)
    {
        expires ??= DateTime.UtcNow.AddMinutes(10);
        var jwt = new JwtSecurityToken(
            new JwtHeader(new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)),
            new JwtPayload(
                issuer,
                IdentityServerPipeline.BaseUrl,
                claims.Select(x => new Claim(x.Key, x.Value)),
                notBefore: null,
                expires: expires));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    /// <summary>
    /// Pushes a plain (non-JAR) authorization request, authenticating via an
    /// HTTP Basic Authorization header. This intentionally bypasses
    /// <see cref="IdentityServerPipeline.PushAuthorizationRequestAsync(Dictionary{string,string})"/>
    /// so that a per-request Authorization header can be set without
    /// leaking auth state across tests via shared default request headers.
    /// </summary>
    private async Task<(JsonDocument, HttpStatusCode)> PushWithBasicAuthAsync(
        string basicClientId,
        string basicClientSecret,
        Dictionary<string, string> form)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, IdentityServerPipeline.ParEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{basicClientId}:{basicClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var httpResponse = await _mockPipeline.BackChannelClient.SendAsync(request);
        var statusCode = httpResponse.StatusCode;
        var rawContent = await httpResponse.Content.ReadAsStringAsync();
        var parsed = rawContent.Length > 0 ? JsonDocument.Parse(rawContent) : null;
        return (parsed, statusCode);
    }

    /// <summary>
    /// Pushes a plain (non-JAR) authorization request, authenticating with a
    /// private_key_jwt client_assertion signed by and valid for
    /// <paramref name="authenticatedClientId"/>.
    /// </summary>
    private async Task<(JsonDocument, HttpStatusCode)> PushWithPrivateKeyJwtAsync(
        string authenticatedClientId,
        WilsonJsonWebKey signingKey,
        Dictionary<string, string> form)
    {
        var now = DateTime.UtcNow;
        var assertion = new JwtSecurityToken(
            issuer: authenticatedClientId,
            audience: IdentityServerPipeline.BaseUrl + "/connect/par",
            claims: new[]
            {
                new Claim(JwtClaimTypes.Subject, authenticatedClientId),
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.IssuedAt, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            },
            notBefore: now,
            expires: now.AddMinutes(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        var assertionValue = new JwtSecurityTokenHandler().WriteToken(assertion);

        var parameters = new Dictionary<string, string>(form)
        {
            { "client_assertion_type", OidcConstants.ClientAssertionTypes.JwtBearer },
            { "client_assertion", assertionValue }
        };

        var httpResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.ParEndpoint, new FormUrlEncodedContent(parameters));
        var statusCode = httpResponse.StatusCode;
        var rawContent = await httpResponse.Content.ReadAsStringAsync();
        var parsed = rawContent.Length > 0 ? JsonDocument.Parse(rawContent) : null;
        return (parsed, statusCode);
    }

    private void ConfigureScopesAndResources()
    {
        _mockPipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        });
        _mockPipeline.ApiResources.AddRange(new ApiResource[] {
            new ApiResource
            {
                Name = "api",
                Scopes = { "api1", "api2" }
            }
        });
        _mockPipeline.ApiScopes.AddRange(new ApiScope[] {
            new ApiScope
            {
                Name = "api1"
            },
            new ApiScope
            {
                Name = "api2"
            }
        });
    }

    private void ConfigureUsers()
    {
        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Claims = new Claim[]
                    {
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com"),
                new Claim("role", "Attorney")
                    }
        });
    }

    private void ConfigureClients()
    {
        _mockPipeline.Clients.AddRange(new Client[]
        {
            _client = new Client
            {
                ClientId = "client1",
                ClientSecrets = new []
                {
                     new Secret(clientSecret.Sha256())
                },
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,
                RequirePkce = false,
                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://client1/callback" },
            },
            _client2 = new Client
            {
                ClientId = "client2",
                ClientSecrets = new []
                {
                    new Secret("secret".Sha256()),
                    new Secret(JsonSerializer.Serialize(_publicKey))
                    {
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey
                    }
                },
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,
                RequirePkce = false,
                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://client2/callback" },
            },
            _publicClient = new Client
            {
                ClientId = PublicClientId,
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,
                RequirePkce = false,
                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://par-public-client/callback" },
            },
            _privateKeyJwtClient = new Client
            {
                ClientId = PrivateKeyJwtClientId,
                ClientSecrets = new []
                {
                    new Secret(JsonSerializer.Serialize(_privateKeyJwtPublicKey))
                    {
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey
                    }
                },
                AllowedGrantTypes = GrantTypes.Code,
                RequireConsent = false,
                RequirePkce = false,
                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://par-private-key-jwt-client/callback" },
            },
        });
    }

    private void ConfigureClientKeys()
    {
        var rsaKey = CryptoHelper.CreateRsaSecurityKey();
        _privateKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        _publicKey = new JsonWebKey
        {
            kty = "RSA",
            use = "sig",
            kid = rsaKey.KeyId,
            e = _privateKey.E,
            n = _privateKey.N,
            alg = _privateKey.Alg
        };

        var privateKeyJwtRsaKey = CryptoHelper.CreateRsaSecurityKey();
        _privateKeyJwtSigningKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(privateKeyJwtRsaKey);
        _privateKeyJwtPublicKey = new JsonWebKey
        {
            kty = "RSA",
            use = "sig",
            kid = privateKeyJwtRsaKey.KeyId,
            e = _privateKeyJwtSigningKey.E,
            n = _privateKeyJwtSigningKey.N,
            alg = _privateKeyJwtSigningKey.Alg
        };
    }


}

