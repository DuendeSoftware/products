// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Default;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Test;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

public class ConsentTests
{
    private const string Category = "Authorize and consent tests";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ConsentTests()
    {
        _mockPipeline.Clients.AddRange(new Client[] {
            new Client
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
                IdentityProviderRestrictions = new List<string> { "google" }
            }
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

        _mockPipeline.ApiScopes.AddRange(new ApiScope[]
        {
            new ApiScope
            {
                Name = "api1"
            },
            new ApiScope
            {
                Name = "api2"
            }
        });

        _mockPipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_requires_consent_should_show_consent_page()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce"
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ConsentWasCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task consent_page_should_have_authorization_params(Type storeType)
    {
        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
            _mockPipeline.Initialize();
        }

        var user = new IdentityServerUser("bob") { Tenant = "tenant_value" };
        await _mockPipeline.LoginAsync(user.CreatePrincipal());

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid api1 api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce",
            acrValues: "acr_1 acr_2 tenant:tenant_value",
            extra: new
            {
                display = "popup", // must use a valid value form the spec for display
                ui_locales = "ui_locale_value",
                custom_foo = "foo_value"
            }
        );
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ConsentRequest.ShouldNotBeNull();
        _mockPipeline.ConsentRequest.Client.ClientId.ShouldBe("client2");
        _mockPipeline.ConsentRequest.DisplayMode.ShouldBe("popup");
        _mockPipeline.ConsentRequest.UiLocales.ShouldBe("ui_locale_value");
        _mockPipeline.ConsentRequest.Tenant.ShouldBe("tenant_value");
        _mockPipeline.ConsentRequest.AcrValues.ShouldBe(["acr_1", "acr_2"]);
        _mockPipeline.ConsentRequest.Parameters.AllKeys.ShouldContain("custom_foo");
        _mockPipeline.ConsentRequest.Parameters["custom_foo"].ShouldBe("foo_value");
        _mockPipeline.ConsentRequest.ValidatedResources.RawScopeValues.ShouldBe(["openid", "api1", "api2"], true);
    }

    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task consent_response_should_allow_successful_authorization_response(Type storeType)
    {
        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
            _mockPipeline.Initialize();
        }

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.ConsentResponse = new ConsentResponse()
        {
            ScopesValuesConsented = new string[] { "openid", "api2" }
        };
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid profile api1 api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNull();
        authorization.State.ShouldBe("123_state");
        var scopes = authorization.Scope.Split(' ');
        scopes.ShouldBe(["api2", "openid"], true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task consent_response_should_reject_modified_request_params()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.ConsentResponse = new ConsentResponse()
        {
            ScopesValuesConsented = new string[] { "openid", "api2" }
        };
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid profile api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://server/consent");

        response = await _mockPipeline.BrowserClient.GetAsync(response.Headers.Location.ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("/connect/authorize/callback");

        var modifiedAuthorizeCallback = "https://server" + response.Headers.Location;
        modifiedAuthorizeCallback = modifiedAuthorizeCallback.Replace("api2", "api1%20api2");

        response = await _mockPipeline.BrowserClient.GetAsync(modifiedAuthorizeCallback);
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://server/consent");
    }

    [Fact()]
    [Trait("Category", Category)]
    public async Task consent_response_missing_required_scopes_should_error()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.ConsentResponse = new ConsentResponse()
        {
            ScopesValuesConsented = new string[] { "api2" }
        };
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid profile api1 api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeTrue();
        authorization.Error.ShouldBe("access_denied");
        authorization.State.ShouldBe("123_state");
    }

    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task consent_response_of_temporarily_unavailable_should_return_error_to_client(Type storeType)
    {
        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
            _mockPipeline.Initialize();
        }

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.ConsentResponse = new ConsentResponse()
        {
            Error = AuthorizationError.TemporarilyUnavailable,
            ErrorDescription = "some description"
        };
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid profile api1 api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeTrue();
        authorization.Error.ShouldBe("temporarily_unavailable");
        authorization.ErrorDescription.ShouldBe("some description");
    }

    [Theory]
    [InlineData((Type)null)]
    [InlineData(typeof(QueryStringAuthorizationParametersMessageStore))]
    [InlineData(typeof(DistributedCacheAuthorizationParametersMessageStore))]
    [Trait("Category", Category)]
    public async Task consent_response_of_unmet_authentication_requirements_should_return_error_to_client(Type storeType)
    {
        if (storeType != null)
        {
            _mockPipeline.OnPostConfigureServices += services =>
            {
                services.AddTransient(typeof(IAuthorizationParametersMessageStore), storeType);
            };
            _mockPipeline.Initialize();
        }

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.ConsentResponse = new ConsentResponse()
        {
            Error = AuthorizationError.UnmetAuthenticationRequirements,
            ErrorDescription = "some description"
        };
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "id_token token",
            scope: "openid profile api1 api2",
            redirectUri: "https://client2/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeTrue();
        authorization.Error.ShouldBe("unmet_authentication_requirements");
        authorization.ErrorDescription.ShouldBe("some description");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task legacy_consents_should_apply_and_be_migrated_to_hex_encoding()
    {
        var clientId = "client2";
        var subjectId = "bob";

        // Create and serialize a consent record
        _mockPipeline.Options.PersistentGrants.DataProtectData = false;
        var serializer = _mockPipeline.Resolve<IPersistentGrantSerializer>();
        var serialized = serializer.Serialize(new Consent
        {
            ClientId = clientId,
            SubjectId = subjectId,
            CreationTime = DateTime.UtcNow,
            Scopes = new List<string> { "openid" }
        });

        // Store the consent using the legacy key format
        var persistedGrantStore = _mockPipeline.Resolve<IPersistedGrantStore>();
        var legacyKey = $"{clientId}|{subjectId}:{IdentityServerConstants.PersistedGrantTypes.UserConsent}".Sha256();
        var legacyConsent = new PersistedGrant
        {
            Key = legacyKey,
            Type = IdentityServerConstants.PersistedGrantTypes.UserConsent,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = Guid.NewGuid().ToString(),
            Description = null,
            CreationTime = DateTime.UtcNow,
            Expiration = null,
            ConsumedTime = null,
            Data = serialized
        };
        await persistedGrantStore.StoreAsync(legacyConsent, _ct);

        // Create a session cookie
        await _mockPipeline.LoginAsync("bob");

        // Start a challenge
        var url = _mockPipeline.CreateAuthorizeUrl(
           clientId: "client2",
           responseType: "id_token",
           scope: "openid",
           redirectUri: "https://client2/callback",
           state: "123_state",
           nonce: "123_nonce"
        );
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        // The existing legacy consent should apply - user isn't show consent screen
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");
        _mockPipeline.ConsentWasCalled.ShouldBeFalse();

        // The legacy consent should be migrated to use a new key...

        // Old key shouldn't find anything
        var grant = await persistedGrantStore.GetAsync(legacyKey, _ct);
        grant.ShouldBeNull();

        // New key should
        var hexEncodedKeyNoHash = $"{clientId}|{subjectId}-1:{IdentityServerConstants.PersistedGrantTypes.UserConsent}";
        using (var sha = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(hexEncodedKeyNoHash);
            var hash = sha.ComputeHash(bytes);
            var hexEncodedKey = BitConverter.ToString(hash).Replace("-", "");
            grant = await persistedGrantStore.GetAsync(hexEncodedKey, _ct);
            grant.ShouldNotBeNull();
            grant.ClientId.ShouldBe(clientId);
            grant.SubjectId.ShouldBe(subjectId);
        }
    }
}
