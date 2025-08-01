// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Net;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Default;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.Validation;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Endpoints.Authorize;

public class AuthorizeTests
{
    private const string Category = "Authorize endpoint";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    private Client _client1;

    public AuthorizeTests()
    {
        _mockPipeline.Clients.AddRange(new Client[] {
            _client1 = new Client
            {
                ClientId = "client1",
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,

                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://client1/callback" },
                AllowAccessTokensViaBrowser = true
            },
            new Client
            {
                ClientId = "client2",
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = true,

                AllowedScopes = new List<string> { "openid", "profile", "api1", "api2" },
                RedirectUris = new List<string> { "https://client2/callback" },
                AllowAccessTokensViaBrowser = true
            },
            new Client
            {
                ClientId = "client3",
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,

                AllowedScopes = new List<string> { "openid", "profile", "api1", "api2" },
                RedirectUris = new List<string> { "https://client3/callback" },
                AllowAccessTokensViaBrowser = true,
                EnableLocalLogin = false,
                IdentityProviderRestrictions = new List<string> { "google" }
            },
            new Client
            {
                ClientId = "client4",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequireConsent = false,
                RequirePkce = false,
                AllowedScopes = new List<string> { "openid", "profile", "api1", "api2" },
                RedirectUris = new List<string> { "https://client4/callback" },
            },

        });

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

        _mockPipeline.OnPostConfigureServices += (services) =>
        {
            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo("en-US"),
                    new CultureInfo("nb-NO")
                };

                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
                options.DefaultRequestCulture = new RequestCulture(supportedCultures[0]);
            });
        };

        _mockPipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_request_should_not_return_404()
    {
        var response = await _mockPipeline.BrowserClient.GetAsync(IdentityServerPipeline.AuthorizeEndpoint);

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task post_request_without_form_should_return_415()
    {
        var response = await _mockPipeline.BrowserClient.PostAsync(IdentityServerPipeline.AuthorizeEndpoint, new StringContent("foo"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task post_request_should_return_200()
    {
        var response = await _mockPipeline.BrowserClient.PostAsync(IdentityServerPipeline.AuthorizeEndpoint,
            new FormUrlEncodedContent(
                new Dictionary<string, string> { }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_request_should_not_return_500()
    {
        var response = await _mockPipeline.BrowserClient.GetAsync(IdentityServerPipeline.AuthorizeEndpoint);

        ((int)response.StatusCode).ShouldBeLessThan(500);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task anonymous_user_should_be_redirected_to_login_page()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task signin_request_should_have_authorization_params(Type storeType)
    {
        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
            _mockPipeline.Initialize();
        }

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            loginHint: "login_hint_value",
            acrValues: "acr_1 acr_2 tenant:tenant_value idp:idp_value",
            extra: new
            {
                display = "popup", // must use a valid value from the spec for display
                ui_locales = "ui_locale_value",
                custom_foo = "foo_value"
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(url + "&foo=foo1&foo=foo2");

        _mockPipeline.LoginRequest.ShouldNotBeNull();
        _mockPipeline.LoginRequest.Client.ClientId.ShouldBe("client1");
        _mockPipeline.LoginRequest.DisplayMode.ShouldBe("popup");
        _mockPipeline.LoginRequest.UiLocales.ShouldBe("ui_locale_value");
        _mockPipeline.LoginRequest.IdP.ShouldBe("idp_value");
        _mockPipeline.LoginRequest.Tenant.ShouldBe("tenant_value");
        _mockPipeline.LoginRequest.LoginHint.ShouldBe("login_hint_value");
        _mockPipeline.LoginRequest.AcrValues.ShouldBe(["acr_1", "acr_2"]);
        _mockPipeline.LoginRequest.Parameters.AllKeys.ShouldContain("foo");
        _mockPipeline.LoginRequest.Parameters.GetValues("foo").ShouldBe(["foo1", "foo2"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task signin_response_should_allow_successful_authorization_response()
    {
        _mockPipeline.Subject = new IdentityServerUser("bob").CreatePrincipal();
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNull();
        authorization.State.ShouldBe("123_state");
        authorization.Values.Keys.ShouldNotContain("iss");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task code_success_response_should_have_all_expected_values()
    {
        _mockPipeline.Subject = new IdentityServerUser("bob").CreatePrincipal();
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client4",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://client4/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client4/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldBeNull();
        authorization.AccessToken.ShouldBeNull();
        authorization.Scope.ShouldBeNullOrEmpty();
        authorization.Code.ShouldNotBeNullOrEmpty();
        authorization.State.ShouldBe("123_state");
        authorization.Values["session_state"].ShouldNotBeNullOrEmpty();
        authorization.Values["iss"].ShouldBe("https%3A%2F%2Fserver");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task implicit_success_response_should_have_all_expected_values()
    {
        _mockPipeline.Subject = new IdentityServerUser("bob").CreatePrincipal();
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNullOrEmpty();
        authorization.AccessToken.ShouldNotBeNullOrEmpty();
        authorization.TokenType.ShouldBe("Bearer");
        authorization.ExpiresIn.ShouldBePositive();
        authorization.Scope.ShouldBe("openid profile");
        authorization.State.ShouldBe("123_state");
        authorization.Values["session_state"].ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task authenticated_user_with_valid_request_should_receive_authorization_response()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNull();
        authorization.State.ShouldBe("123_state");
    }

    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task login_response_and_consent_response_should_receive_authorization_response(Type storeType)
    {
        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
            _mockPipeline.Initialize();
        }

        _mockPipeline.Subject = new IdentityServerUser("bob").CreatePrincipal();

        _mockPipeline.ConsentResponse = new ConsentResponse()
        {
            ScopesValuesConsented = new string[] { "openid", "api1", "profile" }
        };

        _mockPipeline.BrowserClient.StopRedirectingAfter = 4;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid profile api1 api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNull();
        authorization.State.ShouldBe("123_state");
        var scopes = authorization.Scope.Split(' ');
        scopes.ShouldBe(["openid", "profile", "api1"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task idp_should_be_passed_to_login_page()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: "idp:google");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
        _mockPipeline.LoginRequest.IdP.ShouldBe("google");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task idp_not_allowed_by_client_should_not_be_passed_to_login_page()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: "idp:facebook");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
        _mockPipeline.LoginRequest.IdP.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task user_idp_not_allowed_by_client_should_cause_login_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
        _mockPipeline.LoginRequest.IdP.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task user_idp_does_not_match_acr_idp_should_cause_login_page()
    {
        var user = new IdentityServerUser("bob") { IdentityProvider = "idp1" };
        await _mockPipeline.LoginAsync(user.CreatePrincipal());

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: "idp:idp2");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
        _mockPipeline.LoginRequest.IdP.ShouldBe("idp2");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_tenant_validation_enabled_user_tenant_does_not_match_acr_tenant_should_cause_login_page()
    {
        _mockPipeline.Options.ValidateTenantOnAuthorization = true;

        var user = new IdentityServerUser("bob") { Tenant = "t1" };
        await _mockPipeline.LoginAsync(user.CreatePrincipal());

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: "tenant:t2");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
        _mockPipeline.LoginRequest.Tenant.ShouldBe("t2");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_tenant_validation_disabled_user_tenant_does_not_match_acr_tenant_should_not_cause_login_page()
    {
        var user = new IdentityServerUser("bob") { Tenant = "t1" };
        await _mockPipeline.LoginAsync(user.CreatePrincipal());

        _mockPipeline.LoginWasCalled = false;
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: "tenant:t2");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task for_invalid_client_error_page_should_not_receive_client_id()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: null,
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://invalid",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.ClientId.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_page_should_receive_client_id()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://invalid",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.ClientId.ShouldBe("client1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_redirect_uri_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://invalid",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("redirect_uri");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_redirect_uri_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://invalid",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_client_id_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1_invalid",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_client_id_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1_invalid",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_redirect_uri_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1_invalid",
            responseType: "id_token",
            scope: "openid",
            //redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("client");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_redirect_uri_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1_invalid",
            responseType: "id_token",
            scope: "openid",
            //redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task malformed_redirect_uri_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1_invalid",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "invalid-uri",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("client");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task disabled_client_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        _client1.Enabled = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task disabled_client_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        _client1.Enabled = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_protocol_for_client_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        _client1.ProtocolType = "invalid";

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("protocol");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_protocol_for_client_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        _client1.ProtocolType = "invalid";

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_response_type_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "invalid",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnsupportedResponseType);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_response_type_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "invalid",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_response_mode_for_flow_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            responseMode: "query",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("response_mode");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_response_mode_for_flow_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            responseMode: "query",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_response_mode_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            responseMode: "invalid",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnsupportedResponseType);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("response_mode");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_response_mode_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            responseMode: "invalid",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_scope_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            //scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("scope");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_scope_should_not_pass_return_url_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            //scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task explicit_response_mode_should_not_be_passed_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            responseMode: "form_post",
            //scope: "openid", // this will cause the error
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task scope_too_long_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: new string('x', 500),
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("scope");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_openid_scope_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("scope");
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("openid");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_not_allowed_access_to_scope_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid email",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("scope");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_nonce_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state"
        //nonce: "123_nonce"
        );
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("nonce");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task nonce_too_long_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: new string('x', 500));
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("nonce");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task locale_too_long_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { ui_locales = new string('x', 500) });
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("ui_locales");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_max_age_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { max_age = "invalid" });
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("max_age");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task negative_max_age_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { max_age = "-10" });
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("max_age");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task login_hint_too_long_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            loginHint: new string('x', 500));
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("login_hint");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task acr_values_too_long_should_show_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: new string('x', 500));
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
        _mockPipeline.ErrorMessage.ErrorDescription.ShouldContain("acr_values");
        _mockPipeline.ErrorMessage.RedirectUri.ShouldBeNull();
        _mockPipeline.ErrorMessage.ResponseMode.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task overlapping_identity_scopes_and_api_scopes_should_show_error_page()
    {
        _mockPipeline.IdentityScopes.Add(new IdentityResource("foo", "Foo", new string[] { "name" }));
        _mockPipeline.IdentityScopes.Add(new IdentityResource("bar", "Bar", new string[] { "name" }));
        _mockPipeline.ApiScopes.Add(new ApiScope("foo", "Foo"));
        _mockPipeline.ApiScopes.Add(new ApiScope("bar", "Bar"));

        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid foo bar",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        Func<Task> a = () => _mockPipeline.BrowserClient.GetAsync(url);
        await a.ShouldThrowAsync<Exception>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ui_locales_should_be_passed_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: new string('x', 500),
            extra: new { ui_locales = "fr-FR" });
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.UiLocales.ShouldBe("fr-FR");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task display_mode_should_be_passed_to_error_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: new string('x', 500),
            extra: new { display = "popup" });
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.DisplayMode.ShouldBe("popup");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unicode_values_in_url_should_be_processed_correctly()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        url = url.Replace(IdentityServerPipeline.BaseUrl, "https://грант.рф");

        var result = await _mockPipeline.BackChannelClient.GetAsync(url);
        result.Headers.Location.Authority.ShouldBe("xn--80af5akm.xn--p1ai");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task code_flow_with_fragment_response_type_should_be_allowed()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client4",
            responseType: "code",
            responseMode: "fragment",
            scope: "openid",
            redirectUri: "https://client4/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _mockPipeline.BrowserClient.GetAsync(url);
        _mockPipeline.LoginWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_login_should_show_login_page_and_preserve_prompt_values()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "login" }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
        _mockPipeline.LoginRequest.PromptModes.ShouldContain("login");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task max_age_0_should_show_login_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { max_age = "0" }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_login_should_allow_user_to_login_and_complete_authorization()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "login" }
        );

        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        // this simulates the login page returning to the returnUrl which is the authorize callback page
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        response = await _mockPipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + _mockPipeline.LoginReturnUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");
        response.Headers.Location.ToString().ShouldContain("id_token=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task max_age_0_should_allow_user_to_login_and_complete_authorization()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { max_age = "0" }
        );

        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        // this simulates the login page returning to the returnUrl which is the authorize callback page
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        response = await _mockPipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + _mockPipeline.LoginReturnUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");
        response.Headers.Location.ToString().ShouldContain("id_token=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unknown_prompt_should_error()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "unknown" }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task without_config_prompt_create_should_be_treated_as_unknown()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "create" }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task with_config_prompt_create_should_show_create_account_page_and_preserve_prompt_values()
    {
        _mockPipeline.OnPreConfigureServices += svcs =>
        {
            svcs.PostConfigure<IdentityServerOptions>(opts =>
            {
                opts.UserInteraction.CreateAccountUrl = "/account/create";
            });
        };
        _mockPipeline.Initialize();

        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "create" }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.CreateAccountWasCalled.ShouldBeTrue();
        _mockPipeline.CreateAccountRequest.PromptModes.ShouldContain("create");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_create_and_login_should_be_an_error()
    {
        _mockPipeline.OnPreConfigureServices += svcs =>
        {
            svcs.PostConfigure<IdentityServerOptions>(opts =>
            {
                opts.UserInteraction.CreateAccountUrl = "/account/create";
            });
        };
        _mockPipeline.Initialize();

        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client3",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client3/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "create login" }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
    }


    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task custom_request_should_have_authorization_params(Type storeType)
    {
        var mockAuthzInteractionService = new MockAuthzInteractionService();
        mockAuthzInteractionService.Response.RedirectUrl = "/custom";

        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizeInteractionResponseGenerator), svc => mockAuthzInteractionService);
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
        }
        else
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizeInteractionResponseGenerator), svc => mockAuthzInteractionService);
            };
        }
        _mockPipeline.Initialize();


        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            loginHint: "login_hint_value",
            acrValues: "acr_1 acr_2 tenant:tenant_value idp:idp_value",
            extra: new
            {
                display = "popup", // must use a valid value from the spec for display
                ui_locales = "ui_locale_value",
                custom_foo = "foo_value"
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(url + "&foo=bar");

        _mockPipeline.CustomWasCalled.ShouldBeTrue();

        _mockPipeline.CustomRequest.ShouldNotBeNull();
        _mockPipeline.CustomRequest.Client.ClientId.ShouldBe("client1");
        _mockPipeline.CustomRequest.DisplayMode.ShouldBe("popup");
        _mockPipeline.CustomRequest.UiLocales.ShouldBe("ui_locale_value");
        _mockPipeline.CustomRequest.IdP.ShouldBe("idp_value");
        _mockPipeline.CustomRequest.Tenant.ShouldBe("tenant_value");
        _mockPipeline.CustomRequest.LoginHint.ShouldBe("login_hint_value");
        _mockPipeline.CustomRequest.AcrValues.ShouldBe(["acr_1", "acr_2"]);
        _mockPipeline.CustomRequest.Parameters.AllKeys.ShouldContain("foo");
        _mockPipeline.CustomRequest.Parameters["foo"].ShouldBe("bar");
    }

    [Fact]
    public async Task custom_prompt_values_should_raise_error_with_default_interaction_service()
    {
        _mockPipeline.Options.UserInteraction.PromptValuesSupported.Add("custom-prompt");
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "custom-prompt" }
        );

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        Func<Task> a = () => _mockPipeline.BrowserClient.GetAsync(url);
        await a.ShouldThrowAsync<Exception>();
    }

    [Fact]
    public async Task custom_prompt_value_should_be_passed_to_custom_interaction_service()
    {
        var mockAuthzInteractionService = new MockAuthzInteractionService();
        mockAuthzInteractionService.Response.RedirectUrl = "/custom";
        _mockPipeline.OnPostConfigureServices += services =>
        {
            services.AddTransient(typeof(IAuthorizeInteractionResponseGenerator), svc => mockAuthzInteractionService);
        };
        _mockPipeline.Initialize();

        _mockPipeline.Options.UserInteraction.PromptValuesSupported.Add("custom-prompt");

        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid profile",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new { prompt = "custom-prompt" }
        );

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var response = await _mockPipeline.BrowserClient.GetAsync(url);
        response.Headers.Location.GetLeftPart(UriPartial.Path).ShouldBe("https://server/custom");
        mockAuthzInteractionService.Request.PromptModes.ShouldContain("custom-prompt");
        mockAuthzInteractionService.Request.PromptModes.Count().ShouldBe(1);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task redirect_to_login_should_set_cookie_from_ui_locales_when_ui_locales_parameter_is_populated()
    {
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new
            {
                ui_locales = "nb-NO"
            });

        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();

        var cookie = _mockPipeline.BrowserClient.GetCookie("https://server", CookieRequestCultureProvider.DefaultCookieName);
        cookie.ShouldNotBeNull();
        cookie.Value.ShouldBe(Uri.EscapeDataString(CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo("nb-NO")))));
    }

    [Fact]
    public async Task redirect_to_consent_should_set_cookie_from_ui_locales_when_ui_locales_parameter_is_populated()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new
            {
                prompt = "consent",
                ui_locales = "nb-NO"
            });

        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ConsentWasCalled.ShouldBeTrue();

        var cookie = _mockPipeline.BrowserClient.GetCookie("https://server", CookieRequestCultureProvider.DefaultCookieName);
        cookie.ShouldNotBeNull();
        cookie.Value.ShouldBe(Uri.EscapeDataString(CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo("nb-NO")))));
    }

    [Fact]
    public async Task
        redirect_to_create_account_should_set_cookie_from_ui_locales_when_ui_locales_parameter_is_populated()
    {
        _mockPipeline.OnPreConfigureServices += svcs =>
        {
            svcs.PostConfigure<IdentityServerOptions>(opts =>
            {
                opts.UserInteraction.CreateAccountUrl = "/account/create";
            });
        };
        _mockPipeline.Initialize();

        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce",
            extra: new
            {
                prompt = "create",
                ui_locales = "nb-NO"
            });

        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.CreateAccountWasCalled.ShouldBeTrue();

        var cookie = _mockPipeline.BrowserClient.GetCookie("https://server", CookieRequestCultureProvider.DefaultCookieName);
        cookie.ShouldNotBeNull();
        cookie.Value.ShouldBe(Uri.EscapeDataString(CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo("nb-NO")))));
    }
}

public class MockAuthzInteractionService : IAuthorizeInteractionResponseGenerator
{
    public InteractionResponse Response { get; set; } = new InteractionResponse();
    public ValidatedAuthorizeRequest Request { get; internal set; }

    public Task<InteractionResponse> ProcessInteractionAsync(ValidatedAuthorizeRequest request, ConsentResponse consent = null)
    {
        Request = request;
        return Task.FromResult(Response);
    }
}
