// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class ExtensionGrantClient
{
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public ExtensionGrantClient()
    {
        var builder = new WebHostBuilder()
            .UseStartup<Startup>();
        var server = new TestServer(builder);

        _client = server.CreateClient();
    }

    [Fact]
    public async Task Valid_client_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"},
                { "scope", "api1" }
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.custom");
        payload["sub"].GetString().ShouldBe("818727");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("custom");
    }

    [Fact]
    public async Task Valid_client_with_extra_claim_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"},
                { "extra_claim", "extra_value" },
                { "scope", "api1" }
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = payload["exp"].GetInt64();
        exp.ShouldBeLessThan(unixNow + 3620);
        exp.ShouldBeGreaterThan(unixNow + 3580);

        payload.Count.ShouldBe(13);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.custom");
        payload["sub"].GetString().ShouldBe("818727");
        payload["idp"].GetString().ShouldBe("local");
        payload["extra_claim"].GetString().ShouldBe("extra_value");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("custom");

    }

    [Fact]
    public async Task Valid_client_with_refreshed_extra_claim_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"},
                { "extra_claim", "extra_value" },
                { "scope", "api1 offline_access" }
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldNotBeNull();

        var refreshResponse = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,

            ClientId = "client.custom",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        });

        refreshResponse.IsError.ShouldBeFalse();
        refreshResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        refreshResponse.ExpiresIn.ShouldBe(3600);
        refreshResponse.TokenType.ShouldBe("Bearer");
        refreshResponse.IdentityToken.ShouldBeNull();
        refreshResponse.RefreshToken.ShouldNotBeNull();

        var payload = GetPayload(refreshResponse);

        var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = payload["exp"].GetInt64();
        exp.ShouldBeLessThan(unixNow + 3620);
        exp.ShouldBeGreaterThan(unixNow + 3580);

        payload.Count.ShouldBe(13);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.custom");
        payload["sub"].GetString().ShouldBe("818727");
        payload["idp"].GetString().ShouldBe("local");
        payload["extra_claim"].GetString().ShouldBe("extra_value");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("custom");
    }

    [Fact]
    public async Task Valid_client_no_subject_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom.nosubject",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"},
                { "scope", "api1" }
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(8);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.custom");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");
    }

    [Fact]
    public async Task Valid_client_with_default_scopes_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"}
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldNotBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.custom");
        payload["sub"].GetString().ShouldBe("818727");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("custom");

        var scopes = payload["scope"].EnumerateArray();
        scopes.Count().ShouldBe(3);
        scopes.First().ToString().ShouldBe("api1");
        scopes.Skip(1).First().ToString().ShouldBe("api2");
        scopes.Skip(2).First().ToString().ShouldBe("offline_access");
    }

    [Fact]
    public async Task Valid_client_missing_grant_specific_data_should_fail()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" }
            }
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
        response.ErrorDescription.ShouldBe("invalid_custom_credential");
    }

    [Fact]
    public async Task Valid_client_using_unsupported_grant_type_should_fail()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "invalid",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"},
                { "scope", "api1" }
            }
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("unsupported_grant_type");
    }

    [Fact]
    public async Task Valid_client_using_unauthorized_grant_type_should_fail()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom2",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "custom_credential", "custom credential"},
                { "scope", "api1" }
            }
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("unsupported_grant_type");
    }

    [Fact(Skip = "needs improvement")]
    public async Task Dynamic_lifetime_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "dynamic",

            ClientId = "client.dynamic",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },

                { "lifetime", "5000"},
                { "sub",  "818727"}
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(5000);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = payload["exp"].GetInt64();
        exp.ShouldBeLessThan(unixNow + 5020);
        exp.ShouldBeGreaterThan(unixNow + 4980);

        payload.Count.ShouldBe(10);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.dynamic");
        payload["sub"].GetString().ShouldBe("88421113");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("delegation");
    }

    [Fact]
    public async Task Dynamic_token_type_jwt_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "dynamic",

            ClientId = "client.dynamic",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },

                { "type", "jwt"},
                { "sub",  "818727"}
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        response.AccessToken.ShouldContain(".");
    }

    [Fact]
    public async Task Impersonate_client_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "dynamic",

            ClientId = "client.dynamic",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },

                { "type", "jwt"},
                { "impersonated_client", "impersonated_client_id"},
                { "sub",  "818727"}
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        response.AccessToken.ShouldContain(".");

        var jwt = new JwtSecurityToken(response.AccessToken);
        jwt.Payload["client_id"].ShouldBe("impersonated_client_id");
    }

    [Fact]
    public async Task Dynamic_token_type_reference_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "dynamic",

            ClientId = "client.dynamic",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },

                { "type", "reference"},
                { "sub",  "818727"}
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        response.AccessToken.ShouldNotContain(".");
    }

    [Fact]
    public async Task Dynamic_client_claims_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "dynamic",

            ClientId = "client.dynamic",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },

                { "claim", "extra_claim"},
                { "sub",  "818727"}
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(13);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.dynamic");
        payload["sub"].GetString().ShouldBe("818727");
        payload["idp"].GetString().ShouldBe("local");
        payload["client_extra"].GetString().ShouldBe("extra_claim");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("delegation");
    }

    [Fact]
    public async Task Dynamic_client_claims_no_sub_should_succeed()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "dynamic",

            ClientId = "client.dynamic",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },

                { "claim", "extra_claim"},
            }
        });

        response.IsError.ShouldBeFalse();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(9);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.dynamic");
        payload["client_extra"].GetString().ShouldBe("extra_claim");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");
    }

    private Dictionary<string, JsonElement> GetPayload(TokenResponse response)
    {
        var token = response.AccessToken.Split('.').Skip(1).Take(1).First();
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Encoding.UTF8.GetString(Base64Url.Decode(token)));

        return dictionary;
    }
}
