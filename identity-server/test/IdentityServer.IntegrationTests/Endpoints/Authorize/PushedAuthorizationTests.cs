// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using IntegrationTests.Common;

namespace IntegrationTests.Endpoints.Authorize;


public class PushedAuthorizationTests
{
    private readonly IdentityServerPipeline _mockPipeline = new();
    private Client _client;

    public PushedAuthorizationTests()
    {
        ConfigureClients();
        ConfigureUsers();
        ConfigureScopesAndResources();

        _mockPipeline.Initialize();

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
        statusCode.ShouldBe(HttpStatusCode.Created);

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            requestUri: parJson.RootElement.GetProperty("request_uri").GetString());
        var response = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.AbsoluteUri.ShouldMatch($"{expectedCallback}.*");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNull();
        authorization.State.ShouldBe(expectedState);
    }

    [Fact]
    public async Task using_pushed_authorization_when_it_is_globally_disabled_fails()
    {
        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = false;

        var (_, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync();
        statusCode.ShouldBe(HttpStatusCode.NotFound);
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
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldMatch(".*/error.*");
    }

    [Fact]
    public async Task not_using_pushed_authorization_when_it_is_required_for_client_fails()
    {
        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint.ShouldBeTrue();
        _mockPipeline.Options.PushedAuthorization.Required.ShouldBeFalse();
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
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldMatch(".*/error.*");
    }

    [Fact]
    public async Task existing_pushed_authorization_request_uris_become_invalid_when_par_is_disabled()
    {
        // PAR is enabled when we push authorization...
        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync();
        statusCode.ShouldBe(HttpStatusCode.Created);
        parJson.ShouldNotBeNull();

        // ... But then is later disabled, and then we try to use the pushed request
        _mockPipeline.Options.Endpoints.EnablePushedAuthorizationEndpoint = false;

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            requestUri: parJson.RootElement.GetProperty("request_uri").GetString());

        // We expect to be redirected to the error page, as this is an interactive
        // call to authorize. We don't want to follow redirects. Instead we'll just
        // check for a 302 to the error page
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var authorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Found);
        authorizeResponse.Headers.Location!.ToString().ShouldMatch(".*/error.*");
    }

    [Fact]
    public async Task reusing_pushed_authorization_request_uris_fails()
    {
        // Login
        await _mockPipeline.LoginAsync("bob");

        var (parJson, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync();
        statusCode.ShouldBe(HttpStatusCode.Created);
        parJson.ShouldNotBeNull();

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            requestUri: parJson.RootElement.GetProperty("request_uri").GetString());

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var firstAuthorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);
        var secondAuthorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        secondAuthorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Found);
        secondAuthorizeResponse.Headers.Location!.ToString().ShouldMatch(".*/error.*");
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
        statusCode.ShouldBe(HttpStatusCode.BadRequest);
        parJson.ShouldNotBeNull();
        parJson.RootElement.GetProperty("error").GetString()
            .ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
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
        statusCode.ShouldBe(HttpStatusCode.Created);

        // Authorize using pushed request
        var authorizeUrl = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            requestUri: parJson.RootElement.GetProperty("request_uri").GetString());
        var authorizeResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeUrl);

        // Verify that authorize redirects to login
        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Found);
        var isPromptCreate = parameterName == "prompt" && parameterValue == "create";
        var expectedLocation = isPromptCreate ? IdentityServerPipeline.CreateAccountPage : IdentityServerPipeline.LoginPage;
        authorizeResponse.Headers.Location.ToString().ToLower().ShouldMatch($"{expectedLocation.ToLower()}*");

        // Verify that the UI prompts the user at this point
        var uiResponse = await _mockPipeline.BrowserClient.GetAsync(authorizeResponse.Headers.Location);
        uiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now login and return to the return url we were given
        var returnPath = isPromptCreate ? _mockPipeline.CreateAccountReturnUrl : _mockPipeline.LoginReturnUrl;
        var returnUrl = new Uri(new Uri(IdentityServerPipeline.BaseUrl), returnPath);
        await _mockPipeline.LoginAsync("bob");
        var authorizeCallbackResponse = await _mockPipeline.BrowserClient.GetAsync(returnUrl);

        // The authorize callback should continue back to the application (the prompt parameter is processed so we don't go back to the UI)
        authorizeCallbackResponse.StatusCode.ShouldBe(HttpStatusCode.Found);
        authorizeCallbackResponse.Headers.Location!.ToString().ShouldStartWith(expectedCallback);
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

    private void ConfigureUsers() => _mockPipeline.Users.Add(new TestUser
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

    private void ConfigureClients() => _mockPipeline.Clients.AddRange(new Client[]
        {
            _client = new Client
            {
                ClientId = "client1",
                ClientSecrets = new []
                {
                     new Secret("secret".Sha256())
                },
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,
                RequirePkce = false,
                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://client1/callback" },
            },
        });


}
