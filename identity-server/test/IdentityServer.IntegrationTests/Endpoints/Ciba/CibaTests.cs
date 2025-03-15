// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
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
using Microsoft.IdentityModel.Tokens;
using static Duende.IdentityServer.IdentityServerConstants;

namespace IdentityServer.IntegrationTests.Endpoints.Ciba;

public class CibaTests
{
    private const string Category = "Backchannel Authentication (CIBA) endpoint";

    private IdentityServerPipeline _mockPipeline = new();
    private MockCibaUserValidator _mockCibaUserValidator = new();
    private MockCibaUserNotificationService _mockCibaUserNotificationService = new();
    private MockCustomBackchannelAuthenticationValidator _mockCustomBackchannelAuthenticationValidator = new();

    private TestUser _user;
    private Client _cibaClient;

    public CibaTests()
    {
        _mockPipeline.OnPostConfigureServices += services =>
        {
            services.AddSingleton<IBackchannelAuthenticationUserValidator>(_mockCibaUserValidator);
            services.AddSingleton<IBackchannelAuthenticationUserNotificationService>(_mockCibaUserNotificationService);
            services.AddSingleton<ICustomBackchannelAuthenticationValidator>(_mockCustomBackchannelAuthenticationValidator);
        };

        _mockPipeline.Clients.AddRange(new Client[] {
            _cibaClient = new Client
            {
                ClientId = "client1",
                AllowedGrantTypes = GrantTypes.Ciba,
                ClientSecrets =
                {
                    new Secret("secret".Sha256()),
                    new Secret
                    {
                        Type = SecretTypes.JsonWebKey,
                        Value =
                        """
                        {
                            "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                            "kty":"RSA",
                            "e":"AQAB",
                            "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw"
                        }
                        """
                    }
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

    private string CreateRequestObject(IDictionary<string, string> values)
    {
        var claims = new List<Claim>();

        foreach (var item in values)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                claims.Add(new Claim(item.Key, item.Value));
            }
        }

        const string rsaKey =
            """
            {
                "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                "kty":"RSA",
                "d":"GmiaucNIzdvsEzGjZjd43SDToy1pz-Ph-shsOUXXh-dsYNGftITGerp8bO1iryXh_zUEo8oDK3r1y4klTonQ6bLsWw4ogjLPmL3yiqsoSjJa1G2Ymh_RY_sFZLLXAcrmpbzdWIAkgkHSZTaliL6g57vA7gxvd8L4s82wgGer_JmURI0ECbaCg98JVS0Srtf9GeTRHoX4foLWKc1Vq6NHthzqRMLZe-aRBNU9IMvXNd7kCcIbHCM3GTD_8cFj135nBPP2HOgC_ZXI1txsEf-djqJj8W5vaM7ViKU28IDv1gZGH3CatoysYx6jv1XJVvb2PH8RbFKbJmeyUm3Wvo-rgQ",
                "dp":"YNjVBTCIwZD65WCht5ve06vnBLP_Po1NtL_4lkholmPzJ5jbLYBU8f5foNp8DVJBdFQW7wcLmx85-NC5Pl1ZeyA-Ecbw4fDraa5Z4wUKlF0LT6VV79rfOF19y8kwf6MigyrDqMLcH_CRnRGg5NfDsijlZXffINGuxg6wWzhiqqE","dq":"LfMDQbvTFNngkZjKkN2CBh5_MBG6Yrmfy4kWA8IC2HQqID5FtreiY2MTAwoDcoINfh3S5CItpuq94tlB2t-VUv8wunhbngHiB5xUprwGAAnwJ3DL39D2m43i_3YP-UO1TgZQUAOh7Jrd4foatpatTvBtY3F1DrCrUKE5Kkn770M",
                "e":"AQAB",
                "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw","p":"7enorp9Pm9XSHaCvQyENcvdU99WCPbnp8vc0KnY_0g9UdX4ZDH07JwKu6DQEwfmUA1qspC-e_KFWTl3x0-I2eJRnHjLOoLrTjrVSBRhBMGEH5PvtZTTThnIY2LReH-6EhceGvcsJ_MhNDUEZLykiH1OnKhmRuvSdhi8oiETqtPE","q":"0CBLGi_kRPLqI8yfVkpBbA9zkCAshgrWWn9hsq6a7Zl2LcLaLBRUxH0q1jWnXgeJh9o5v8sYGXwhbrmuypw7kJ0uA3OgEzSsNvX5Ay3R9sNel-3Mqm8Me5OfWWvmTEBOci8RwHstdR-7b9ZT13jk-dsZI7OlV_uBja1ny9Nz9ts","qi":"pG6J4dcUDrDndMxa-ee1yG4KjZqqyCQcmPAfqklI2LmnpRIjcK78scclvpboI3JQyg6RCEKVMwAhVtQM6cBcIO3JrHgqeYDblp5wXHjto70HVW6Z8kBruNx1AH9E8LzNvSRL-JVTFzBkJuNgzKQfD0G77tQRgJ-Ri7qu3_9o1M4"
            }
            """;
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            _cibaClient.ClientId,
            IdentityServerPipeline.BaseUrl,
            claims,
            now,
            now.AddMinutes(1),
            new SigningCredentials(new Microsoft.IdentityModel.Tokens.JsonWebKey(rsaKey), "RS256")
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();

        return tokenHandler.WriteToken(token);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_request_should_return_error()
    {
        var response = await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.BackchannelAuthenticationEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task post_request_without_form_should_return_error()
    {
        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new StringContent("invalid"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_values_should_be_passed_to_user_validator()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "user_code", "xoxo" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _mockCibaUserValidator.UserValidatorContext.LoginHint.ShouldBe("this means bob");
        _mockCibaUserValidator.UserValidatorContext.UserCode.ShouldBe("xoxo");
        _mockCibaUserValidator.UserValidatorContext.BindingMessage.ShouldBe(bindingMessage);
        _mockCibaUserValidator.UserValidatorContext.Client.ClientId.ShouldBe(_cibaClient.ClientId);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task custom_validators_are_invoked_and_can_process_custom_input()
    {
        _mockCustomBackchannelAuthenticationValidator.Thunk = ctx =>
        {
            // Map the incoming custom input so that we use it throughout the pipeline
            ctx.ValidationResult.ValidatedRequest.Properties.Add("custom",
                ctx.ValidationResult.ValidatedRequest.Raw["custom"]);
        };

        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "user_code", "xoxo" },
            { "binding_message", bindingMessage },
            
            // This isn't part of any spec or our models, except as custom Properties
            { "custom", "input" }
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        // The custom validator was invoked with the request parameters and mapped the custom input
        var validatedRequest = _mockCustomBackchannelAuthenticationValidator.Context.ValidationResult.ValidatedRequest;
        validatedRequest.ShouldNotBeNull();
        validatedRequest.ClientId.ShouldBe("client1");
        validatedRequest.BindingMessage.ShouldBe(bindingMessage);
        validatedRequest.Properties["custom"].ShouldBe("input");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_validator_can_add_complex_properties_that_are_passed_to_user_notification_but_not_client_response()
    {
        _mockCustomBackchannelAuthenticationValidator.Thunk = ctx =>
            {
                // Invent a nested value, as if there was custom logic doing something "interesting"
                ctx.ValidationResult.ValidatedRequest.Properties.Add("complex",
                    new Dictionary<string, string>
                    {
                        { "nested", "value" },
                    });
            };

        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "user_code", "xoxo" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        // Custom request properties are not included automatically in the response to the client
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);
        json.ShouldNotBeNull();
        json.ShouldNotContainKey("complex");

        // Custom properties are passed to the notification service
        var notificationProperties = _mockCibaUserNotificationService.LoginRequest.Properties;
        var complexObjectInNotification = notificationProperties["complex"] as Dictionary<string, string>;
        complexObjectInNotification.ShouldNotBeNull();
        complexObjectInNotification["nested"].ShouldBe("value");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_request_should_return_valid_result()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("auth_req_id").ShouldBeTrue();
        values.ContainsKey("expires_in").ShouldBeTrue();
        values.ContainsKey("interval").ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_request_object_should_return_valid_result()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("n") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("auth_req_id").ShouldBeTrue();
        values.ContainsKey("expires_in").ShouldBeTrue();
        values.ContainsKey("interval").ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task no_request_object_when_required_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        _cibaClient.RequireRequestObject = true;

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("n") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) + new string('x', 52000) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_invalid_signature_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("n") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) + "invalid_junk" },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_missing_jti_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_with_request_object_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "request", "foo" },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_with_request_uri_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "request_uri", "foo" },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_client_with_no_keys_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        _cibaClient.ClientSecrets.Clear();
        _cibaClient.ClientSecrets.Add(new Secret("secret".Sha256()));

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_client_with_invalid_key_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        _cibaClient.ClientSecrets.Clear();
        _cibaClient.ClientSecrets.Add(new Secret("secret".Sha256()));
        _cibaClient.ClientSecrets.Add(new Secret("invalid") { Type = SecretTypes.JsonWebKey });

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_client_with_mismatched_client_id_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "client_id", "invalid" },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_with_param_also_outside_jwt_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "binding_message", bindingMessage },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request_object");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_client_with_correct_client_id_should_return_success()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var req = new Dictionary<string, string>
        {
            { "jti", Guid.NewGuid().ToString("N") },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "client_id", "client1" },
        };
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "request", CreateRequestObject(req) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_request_should_notify_user()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "resource", "urn:api1" },
            { "acr_values", "foo bar idp:x tenant:y" },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _mockCibaUserNotificationService.LoginRequest.Subject.FindFirst("sub").Value.ShouldBe(_user.SubjectId);
        _mockCibaUserNotificationService.LoginRequest.BindingMessage.ShouldBe(bindingMessage);
        _mockCibaUserNotificationService.LoginRequest.Client.ClientId.ShouldBe(_cibaClient.ClientId);
        _mockCibaUserNotificationService.LoginRequest.RequestedResourceIndicators.ShouldBe(["urn:api1"]);
        _mockCibaUserNotificationService.LoginRequest.AuthenticationContextReferenceClasses.ShouldBe(["bar", "foo"], true);
        _mockCibaUserNotificationService.LoginRequest.IdP.ShouldBe("x");
        _mockCibaUserNotificationService.LoginRequest.Tenant.ShouldBe("y");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_client_credentials_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "invalid" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_client");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_client_grant_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        _cibaClient.AllowedGrantTypes = GrantTypes.ClientCredentials;

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("unauthorized_client");
    }

    [Theory]
    [InlineData("access_denied", HttpStatusCode.Forbidden)]
    [InlineData("expired_login_hint_token", HttpStatusCode.BadRequest)]
    [InlineData("unknown_user_id", HttpStatusCode.BadRequest)]
    [InlineData("missing_user_code", HttpStatusCode.BadRequest)]
    [InlineData("invalid_user_code", HttpStatusCode.BadRequest)]
    [InlineData("invalid_binding_message", HttpStatusCode.BadRequest)]
    [Trait("Category", Category)]
    public async Task invalid_user_errors_should_return_error(string error, HttpStatusCode code)
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        _mockCibaUserValidator.Result.Error = error;

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(code);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe(error);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unknown_user_validator_error_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        _mockCibaUserValidator.Result.Error = "unknown";

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("unknown_user_id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task validated_user_missing_sub_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        var claims = new Claim[] { };
        var ci = new ClaimsIdentity(claims, "ciba");
        _mockCibaUserValidator.Result.Subject = new ClaimsPrincipal(ci);

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("unknown_user_id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_scope_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task scope_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            //{ "scope", "openid profile scope1 offline_access" },
            { "scope", "openid profile scope1 offline_access" + new string('x', 2000) },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task scope_missing_openid_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task resource_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "resource", new string('x', 2000) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task resource_invalid_format_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "resource", "not_a_uri" },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_resource_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "resource", "urn:invalid" },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_scope_for_resource_should_return_error()
    {
        var mockResourceValidator = new MockResourceValidator { };
        mockResourceValidator.Result.InvalidScopes.Add("scope1");
        _mockPipeline.OnPostConfigureServices += s => s.AddSingleton<IResourceValidator>(mockResourceValidator);
        _mockPipeline.Initialize();

        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "resource", "urn:api1" },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_scope");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expiry_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "requested_expiry", "1234567890" },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expiry_larger_than_client_allows_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "requested_expiry", "500" },
        };

        _cibaClient.CibaLifetime = 400;

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expiry_than_than_client_allows_should_return_success()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "requested_expiry", "300" },
        };

        _cibaClient.CibaLifetime = 400;

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task acr_values_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "acr_values", new string('x', 1000) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_with_idp_restriction_should_ignore_invalid_idp_acr_value()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage },
            { "acr_values", "idp:x" },
        };

        SetValidatedUser();

        _cibaClient.IdentityProviderRestrictions = new[] { "y" };

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _mockCibaUserNotificationService.LoginRequest.IdP.ShouldBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task too_many_login_hints_should_return_error()
    {
        {
            var bindingMessage = Guid.NewGuid().ToString("n");
            var body = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "scope", "openid profile scope1 offline_access" },
                { "login_hint", "this means bob" },
                { "login_hint_token", "this means bob" },
                { "binding_message", bindingMessage },
            };

            SetValidatedUser();

            var response = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.BackchannelAuthenticationEndpoint,
                new FormUrlEncodedContent(body));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await response.Content.ReadAsStringAsync();
            var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("invalid_request");
        }
        {
            var bindingMessage = Guid.NewGuid().ToString("n");
            var body = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "scope", "openid profile scope1 offline_access" },
                { "login_hint", "this means bob" },
                { "id_token_hint", "this means bob" },
                { "binding_message", bindingMessage },
            };

            SetValidatedUser();

            var response = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.BackchannelAuthenticationEndpoint,
                new FormUrlEncodedContent(body));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await response.Content.ReadAsStringAsync();
            var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("invalid_request");
        }
        {
            var bindingMessage = Guid.NewGuid().ToString("n");
            var body = new Dictionary<string, string>
            {
                { "client_id", "client1" },
                { "client_secret", "secret" },
                { "scope", "openid profile scope1 offline_access" },
                { "id_token_hint", "this means bob" },
                { "login_hint_token", "this means bob" },
                { "binding_message", bindingMessage },
            };

            SetValidatedUser();

            var response = await _mockPipeline.BackChannelClient.PostAsync(
                IdentityServerPipeline.BackchannelAuthenticationEndpoint,
                new FormUrlEncodedContent(body));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var json = await response.Content.ReadAsStringAsync();
            var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            values.ContainsKey("error").ShouldBeTrue();
            values["error"].ToString().ShouldBe("invalid_request");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task too_few_login_hints_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task login_hint_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" + new string('x', 100) },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task login_hint_token_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint_token", "this means bob" + new string('x', 4000) },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "id_token_hint", "this means bob" + new string('x', 4000) },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_id_token_hint_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "id_token_hint", "invalid" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_id_token_hint_should_return_success()
    {
        _mockPipeline.Options.IssuerUri = IdentityServerPipeline.BaseUrl;

        var tokenService = _mockPipeline.Resolve<IIdentityServerTools>();
        var id_token = await tokenService.IssueJwtAsync(600, new Claim[] {
            new Claim("sub", _user.SubjectId),
            new Claim("aud", _cibaClient.ClientId),
        });

        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "id_token_hint", id_token },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _mockCibaUserNotificationService.LoginRequest.Subject.HasClaim("sub", _user.SubjectId).ShouldBeTrue();
        _mockCibaUserValidator.UserValidatorContext.IdTokenHint.ShouldBe(id_token);
        _mockCibaUserValidator.UserValidatorContext.IdTokenHintClaims.ShouldContain(x => x.Type == "sub" && x.Value == _user.SubjectId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_login_hint_token_hint_should_return_success()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint_token", "xoxo" },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _mockCibaUserNotificationService.LoginRequest.Subject.HasClaim("sub", _user.SubjectId).ShouldBeTrue();
        _mockCibaUserValidator.UserValidatorContext.LoginHintToken.ShouldBe("xoxo");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task user_code_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "user_code", "xoxo" + new string('x', 100) },
            { "binding_message", bindingMessage },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task binding_message_too_long_should_return_error()
    {
        var bindingMessage = Guid.NewGuid().ToString("n");
        var body = new Dictionary<string, string>
        {
            { "client_id", "client1" },
            { "client_secret", "secret" },
            { "scope", "openid profile scope1 offline_access" },
            { "login_hint", "this means bob" },
            { "binding_message", bindingMessage + new string('x', 100) },
        };

        SetValidatedUser();

        var response = await _mockPipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        values.ContainsKey("error").ShouldBeTrue();
        values["error"].ToString().ShouldBe("invalid_binding_message");
    }

}
