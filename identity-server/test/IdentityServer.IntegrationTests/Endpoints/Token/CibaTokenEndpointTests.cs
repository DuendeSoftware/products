// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.Validation;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Endpoints.Token;

public class CibaTokenEndpointTests
{
    private const string Category = "CIBA Token endpoint";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();
    private MockCibaUserValidator _mockCibaUserValidator = new MockCibaUserValidator();
    private MockCibaUserNotificationService _mockCibaUserNotificationService = new MockCibaUserNotificationService();

    private TestUser _user;
    private Client _cibaClient;

    public CibaTokenEndpointTests()
    {
        _mockPipeline.OnPostConfigureServices += services =>
        {
            services.AddSingleton<IBackchannelAuthenticationUserValidator>(_mockCibaUserValidator);
            services.AddSingleton<IBackchannelAuthenticationUserNotificationService>(_mockCibaUserNotificationService);
        };

        _mockPipeline.Clients.AddRange(new Client[] {
            _cibaClient = new Client
            {
                ClientId = "client1",
                AllowedGrantTypes = GrantTypes.Ciba,
                ClientSecrets =
                {
                    new Secret("secret".Sha256()),
                },
                AllowOfflineAccess = true,
                AllowedScopes = new List<string> { "openid", "profile", "scope1" },
            },
            new Client
            {
                ClientId = "client2",
                AllowedGrantTypes = GrantTypes.Ciba,
                ClientSecrets =
                {
                    new Secret("secret".Sha256()),
                },
                AllowOfflineAccess = true,
                AllowedScopes = new List<string> { "openid", "profile", "scope1" },
            },
            new Client
            {
                ClientId = "client3",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets =
                {
                    new Secret("secret".Sha256()),
                },
                AllowOfflineAccess = true,
                AllowedScopes = new List<string> { "openid", "profile", "scope1" },
            },
        });

        _mockPipeline.Users.Add(_user = new TestUser
        {
            SubjectId = "123",
            Username = "bob",
            Password = "bob",
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
                Name = "urn:api1",
                Scopes = { "scope1" }
            },
            new ApiResource
            {
                Name = "urn:api2",
                Scopes = { "scope1" }
            },
        });
        _mockPipeline.ApiScopes.AddRange(new ApiScope[] {
            new ApiScope
            {
                Name = "scope1"
            },
        });

        _mockPipeline.Initialize();
    }

    private void SetValidatedUser(string sub = null)
    {
        sub = sub ?? _user.SubjectId;

        var claims = new Claim[] { new Claim("sub", sub) };
        var ci = new ClaimsIdentity(claims, "ciba");
        _mockCibaUserValidator.Result.Subject = new ClaimsPrincipal(ci);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task valid_request_should_return_valid_result()
    {
        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // user auth/consent
        var cibaService = _mockPipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues,
            Subject = new IdentityServerUser(_user.SubjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }
                .CreatePrincipal()
        });


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task request_before_consent_should_return_error()
    {
        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);



        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("authorization_pending");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_mismatch_should_return_error()
    {
        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // user auth/consent
        var cibaService = _mockPipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues,
            Subject = new IdentityServerUser(_user.SubjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }
                .CreatePrincipal()
        });


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client2" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_grant");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_not_allowed_for_ciba_should_return_error()
    {
        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // user auth/consent
        var cibaService = _mockPipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues,
            Subject = new IdentityServerUser(_user.SubjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }
                .CreatePrincipal()
        });


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client3" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("unauthorized_client");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task user_denies_consent_should_return_error()
    {
        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // user auth/consent
        var cibaService = _mockPipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            //ScopesValuesConsented = request.ValidatedResources.RawScopeValues, // none to deny
            Subject = new IdentityServerUser(_user.SubjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }
                .CreatePrincipal()
        });


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("access_denied");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task user_inactive_should_return_error()
    {
        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // user auth/consent
        var cibaService = _mockPipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues,
            Subject = new IdentityServerUser(_user.SubjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }
                .CreatePrincipal()
        });


        // token request
        _user.IsActive = false;

        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_grant");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expired_request_should_return_error()
    {
        var clock = new MockClock();
        _mockPipeline.OnPostConfigureServices += s => s.AddSingleton<IClock>(clock);
        _mockPipeline.Initialize();

        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // user auth/consent
        var cibaService = _mockPipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues,
            Subject = new IdentityServerUser(_user.SubjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }
                .CreatePrincipal()
        });


        // token request
        clock.UtcNow = DateTimeOffset.UtcNow.AddHours(1);

        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        var tokenBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "grant_type", "urn:openid:params:grant-type:ciba" },
            { "auth_req_id", requestId },
        };

        var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(tokenBody));

        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("expired_token");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task too_frequent_request_should_return_error()
    {
        var clock = new MockClock();
        _mockPipeline.OnPostConfigureServices += s => s.AddSingleton<IClock>(clock);
        _mockPipeline.Initialize();

        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("authorization_pending");
        }
        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("slow_down");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task calls_past_interval_should_reset_throttling()
    {
        var clock = new MockClock();
        _mockPipeline.OnPostConfigureServices += s => s.AddSingleton<IClock>(clock);
        _mockPipeline.Initialize();

        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("authorization_pending");
        }
        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("slow_down");
        }

        clock.UtcNow = clock.UtcNow.AddSeconds(_mockPipeline.Options.Ciba.DefaultPollingInterval);

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("authorization_pending");
        }
        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("slow_down");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_polling_interval_should_be_honored()
    {
        _cibaClient.PollingInterval = 10;

        var clock = new MockClock();
        _mockPipeline.OnPostConfigureServices += s => s.AddSingleton<IClock>(clock);
        _mockPipeline.Initialize();

        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("authorization_pending");
        }
        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("slow_down");
        }

        clock.UtcNow = clock.UtcNow.AddSeconds(6);

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("slow_down");
        }

        clock.UtcNow = clock.UtcNow.AddSeconds(10);

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("authorization_pending");
        }
        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("slow_down");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_lifetime_should_be_honored()
    {
        _cibaClient.CibaLifetime = 100;

        var clock = new MockClock();
        _mockPipeline.OnPostConfigureServices += s => s.AddSingleton<IClock>(clock);
        _mockPipeline.Initialize();

        // ciba request
        var bindingMessage = Guid.NewGuid().ToString("n");
        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var cibaResponse = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);


        // token request
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        var requestId = values["auth_req_id"].ToString();

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("authorization_pending");
        }

        clock.UtcNow = clock.UtcNow.AddSeconds(_cibaClient.CibaLifetime.Value + 1);

        {
            var tokenBody = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            };

            var tokenResponse = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.TokenEndpoint,
                new FormUrlEncodedContent(tokenBody));

            tokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await tokenResponse.Content.ReadAsStringAsync();
            values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("expired_token");
        }
    }
}
