// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics.Metrics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.Endpoints.Introspection.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Introspection;

public class IntrospectionTests
{
    private const string Category = "Introspection endpoint";
    private const string IntrospectionEndpoint = "https://server/connect/introspect";
    private const string TokenEndpoint = "https://server/connect/token";
    private const string RevocationEndpoint = "https://server/connect/revocation";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HttpClient _client;
    private readonly HttpMessageHandler _handler;

    public IntrospectionTests()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseStartup<Startup>();
            });

        var host = hostBuilder.Start();

        _handler = host.GetTestServer().CreateHandler();
        _client = host.GetTestClient();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Empty_request_should_fail()
    {
        var form = new Dictionary<string, string>();

        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_scope_should_fail()
    {
        var form = new Dictionary<string, string>();

        _client.SetBasicAuthentication("unknown", "invalid");
        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_scope_secret_should_fail()
    {
        var form = new Dictionary<string, string>();

        _client.SetBasicAuthentication("api1", "invalid");
        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_token_should_fail()
    {
        var form = new Dictionary<string, string>();

        _client.SetBasicAuthentication("api1", "secret");
        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_token_should_fail()
    {
        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = "invalid"
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(false);
        introspectionResponse.IsError.ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Content_type_should_fail()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var data = new
        {
            client_id = "api1",
            client_secret = "secret",
            token = tokenResponse.AccessToken
        };
        var json = JsonSerializer.Serialize(data);

        var client = new HttpClient(_handler);
        var response = await client.PostAsync(IntrospectionEndpoint,
            new StringContent(json, Encoding.UTF8, "application/json"), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_token_type_hint_should_not_fail()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = "invalid"
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(true);
        introspectionResponse.IsError.ShouldBe(false);

        var scopes = from c in introspectionResponse.Claims
                     where c.Type == "scope"
                     select c;

        scopes.Count().ShouldBe(1);
        scopes.First().Value.ShouldBe("api1");
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("ro.client", Constants.TokenTypeHints.RefreshToken)]
    [InlineData("ro.client", Constants.TokenTypeHints.AccessToken)]
    [InlineData("ro.client", "bogus")]
    [InlineData("api1", Constants.TokenTypeHints.RefreshToken)]
    [InlineData("api1", Constants.TokenTypeHints.AccessToken)]
    [InlineData("api1", "bogus")]
    public async Task Access_tokens_can_be_introspected_with_any_hint(string introspectedBy, string hint)
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = introspectedBy,
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = hint
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(true);
        introspectionResponse.IsError.ShouldBe(false);

        var scopes = from c in introspectionResponse.Claims
                     where c.Type == "scope"
                     select c.Value;
        scopes.ShouldContain("api1");
    }

    [Theory]
    [Trait("Category", Category)]

    // Validate that refresh tokens can be introspected with any hint by the client they were issued to
    [InlineData("ro.client", Constants.TokenTypeHints.RefreshToken, true)]
    [InlineData("ro.client", Constants.TokenTypeHints.AccessToken, true)]
    [InlineData("ro.client", "bogus", true)]

    // Validate that APIs cannot introspect refresh tokens and that we always return isActive: false
    [InlineData("api1", Constants.TokenTypeHints.RefreshToken, false)]
    [InlineData("api1", Constants.TokenTypeHints.AccessToken, false)]
    [InlineData("api1", "bogus", false)]

    public async Task Refresh_tokens_can_be_introspected_by_their_client_with_any_hint(string introspectedBy,
        string hint, bool isActive)
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = introspectedBy,
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken,
            TokenTypeHint = hint
        }, _ct);

        if (isActive)
        {
            introspectionResponse.IsActive.ShouldBe(true);
            introspectionResponse.IsError.ShouldBe(false);

            var scopes = from c in introspectionResponse.Claims
                         where c.Type == "scope"
                         select c;

            scopes.Count().ShouldBe(2);
            scopes.First().Value.ShouldBe("api1");
        }
        else
        {
            introspectionResponse.IsActive.ShouldBe(false);
            introspectionResponse.IsError.ShouldBe(false);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_token_and_valid_scope_should_succeed()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(true);
        introspectionResponse.IsError.ShouldBe(false);

        var scopes = from c in introspectionResponse.Claims
                     where c.Type == "scope"
                     select c;

        scopes.Count().ShouldBe(1);
        scopes.First().Value.ShouldBe("api1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Response_data_should_be_valid_using_single_scope()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        var values = GetFields(introspectionResponse);

        values["iss"].ValueKind.ShouldBe(JsonValueKind.String);
        values["aud"].ValueKind.ShouldBe(JsonValueKind.String);
        values["nbf"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["exp"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["client_id"].ValueKind.ShouldBe(JsonValueKind.String);
        values["active"].ValueKind.ShouldBe(JsonValueKind.True);
        values["scope"].ValueKind.ShouldBe(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().ShouldBe("api1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Response_data_with_user_authentication_should_be_valid_using_single_scope()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",

            Scope = "api1",
        }, _ct);

        tokenResponse.IsError.ShouldBeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        var values = GetFields(introspectionResponse);

        values["iss"].ValueKind.ShouldBe(JsonValueKind.String);
        values["aud"].ValueKind.ShouldBe(JsonValueKind.String);
        values["nbf"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["exp"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["client_id"].ValueKind.ShouldBe(JsonValueKind.String);
        values["active"].ValueKind.ShouldBe(JsonValueKind.True);
        values["scope"].ValueKind.ShouldBe(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().ShouldBe("api1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Response_data_should_be_valid_using_multiple_scopes_multiple_audiences()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Scope = "api2 api3-a api3-b",
        }, _ct);

        tokenResponse.IsError.ShouldBeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api3",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        var values = GetFields(introspectionResponse);

        values["aud"].ValueKind.ShouldBe(JsonValueKind.Array);

        var audiences = values["aud"].EnumerateArray();
        foreach (var aud in audiences)
        {
            aud.ValueKind.ShouldBe(JsonValueKind.String);
        }

        values["iss"].ValueKind.ShouldBe(JsonValueKind.String);
        values["nbf"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["exp"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["client_id"].ValueKind.ShouldBe(JsonValueKind.String);
        values["active"].ValueKind.ShouldBe(JsonValueKind.True);
        values["scope"].ValueKind.ShouldBe(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().ShouldBe("api3-a api3-b");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Response_data_should_be_valid_using_multiple_scopes_single_audience()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Scope = "api3-a api3-b",
        }, _ct);

        tokenResponse.IsError.ShouldBeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api3",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        var values = GetFields(introspectionResponse);

        values["iss"].ValueKind.ShouldBe(JsonValueKind.String);
        values["aud"].ValueKind.ShouldBe(JsonValueKind.String);
        values["nbf"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["exp"].ValueKind.ShouldBe(JsonValueKind.Number);
        values["client_id"].ValueKind.ShouldBe(JsonValueKind.String);
        values["active"].ValueKind.ShouldBe(JsonValueKind.True);
        values["scope"].ValueKind.ShouldBe(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().ShouldBe("api3-a api3-b");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Token_with_many_scopes_but_api_should_only_see_its_own_scopes()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client3",
            ClientSecret = "secret",

            Scope = "api1 api2 api3-a",
        }, _ct);

        tokenResponse.IsError.ShouldBeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api3",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeTrue();
        introspectionResponse.IsError.ShouldBeFalse();

        var scopes = from c in introspectionResponse.Claims
                     where c.Type == "scope"
                     select c.Value;

        scopes.Count().ShouldBe(1);
        scopes.First().ShouldBe("api3-a");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_token_with_valid_multiple_scopes()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Scope = "api1 api2",
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(true);
        introspectionResponse.IsError.ShouldBe(false);

        var scopes = from c in introspectionResponse.Claims
                     where c.Type == "scope"
                     select c;

        scopes.Count().ShouldBe(1);
        scopes.First().Value.ShouldBe("api1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_token_with_invalid_scopes_should_fail()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Scope = "api1",
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api2",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(false);
        introspectionResponse.IsError.ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_validation_with_access_token_should_succeed()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeTrue();
        introspectionResponse.IsError.ShouldBeFalse();
        introspectionResponse.Claims.Single(x => x.Type == "client_id").Value.ShouldBe("client1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_validation_with_refresh_token_should_succeed()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeTrue();
        introspectionResponse.IsError.ShouldBeFalse();
        introspectionResponse.Claims.Single(x => x.Type == "client_id").Value.ShouldBe("ro.client");
        introspectionResponse.Claims.Single(x => x.Type == "sub").Value.ShouldBe("1");
        introspectionResponse.Claims.Where(x => x.Type == "scope").Select(x => x.Value)
            .ShouldBe(["api1", "offline_access"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task api_validation_with_refresh_token_should_fail()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeFalse();
        introspectionResponse.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_validation_with_revoked_refresh_token_should_fail()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var revocationResponse = await _client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = RevocationEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        }, _ct);
        revocationResponse.IsError.ShouldBeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeFalse();
        introspectionResponse.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_validation_with_access_token_for_different_client_should_fail()
    {
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "client2",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeFalse();
        introspectionResponse.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_validation_with_refresh_token_for_different_client_should_fail()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client2",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBeFalse();
        introspectionResponse.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task jwt_response_type_requested_returns_jwt_response()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = Constants.TokenTypeHints.AccessToken,
            ResponseFormat = ResponseFormat.Jwt
        }, _ct);

        introspectionResponse.HttpResponse.Content.Headers.ContentType.MediaType.ShouldBe($"application/{JwtClaimTypes.JwtTypes.IntrospectionJwtResponse}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task jwt_response_type_returns_expected_required_jwt_structure()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = Constants.TokenTypeHints.AccessToken,
            ResponseFormat = ResponseFormat.Jwt
        }, _ct);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(introspectionResponse.Raw);
        jwt.Header.Typ.ShouldBe(JwtClaimTypes.JwtTypes.IntrospectionJwtResponse);
        jwt.Audiences.SingleOrDefault(aud => aud == "ro.client").ShouldNotBeNull();
        jwt.Claims.SingleOrDefault(claim => claim.Type == JwtClaimTypes.Expiration).ShouldBeNull();
        jwt.Claims.SingleOrDefault(claim => claim.Type == JwtClaimTypes.Subject).ShouldBeNull();

        var tokenIntrospectionClaim = jwt.Claims.SingleOrDefault(claim => claim.Type == "token_introspection");
        tokenIntrospectionClaim.ShouldNotBeNull();
        tokenIntrospectionClaim.ValueType.ShouldBe(IdentityServerConstants.ClaimValueTypes.Json, StringCompareShould.IgnoreCase);
        var introspectionFields = tokenIntrospectionClaim.Value.GetFields();
        introspectionFields["active"].GetBoolean().ShouldBeTrue();
        introspectionFields["scope"].GetString().ShouldBe("api1 offline_access");
        introspectionFields["iss"].GetString().ShouldBe("https://idsvr4");
        introspectionFields["aud"].GetString().ShouldBe("api1");
        introspectionFields["client_id"].GetString().ShouldBe("ro.client");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task jwt_response_type_for_revoked_token_only_includes_active_claim_in_introspection_claim()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        }, _ct);

        var revocationResponse = await _client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = RevocationEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        }, _ct);
        revocationResponse.IsError.ShouldBeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = Constants.TokenTypeHints.AccessToken,
            ResponseFormat = ResponseFormat.Jwt
        }, _ct);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(introspectionResponse.Raw);

        var tokenIntrospectionClaim = jwt.Claims.SingleOrDefault(claim => claim.Type == "token_introspection");
        tokenIntrospectionClaim.ShouldNotBeNull();
        tokenIntrospectionClaim.ValueType.ShouldBe(IdentityServerConstants.ClaimValueTypes.Json, StringCompareShould.IgnoreCase);
        var introspectionFields = tokenIntrospectionClaim.Value.GetFields();
        introspectionFields["active"].GetBoolean().ShouldBeFalse();
        introspectionFields.Keys.Count(key => key != "active").ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task jwt_response_type_for_invalid_token_only_includes_active_claim_in_introspection_claim()
    {
        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = "invalid",
            ResponseFormat = ResponseFormat.Jwt
        }, _ct);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(introspectionResponse.Raw);

        var tokenIntrospectionClaim = jwt.Claims.SingleOrDefault(claim => claim.Type == "token_introspection");
        tokenIntrospectionClaim.ShouldNotBeNull();
        tokenIntrospectionClaim.ValueType.ShouldBe(IdentityServerConstants.ClaimValueTypes.Json, StringCompareShould.IgnoreCase);
        var introspectionFields = tokenIntrospectionClaim.Value.GetFields();
        introspectionFields["active"].GetBoolean().ShouldBeFalse();
        introspectionFields.Keys.Count(key => key != "active").ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task jwt_response_type_should_handle_nested_claim_in_introspection_claim()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Scope = "api1 offline_access roles address",
            UserName = "bob",
            Password = "bob"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = Constants.TokenTypeHints.AccessToken,
            ResponseFormat = ResponseFormat.Jwt
        }, _ct);

        introspectionResponse.Json.ShouldNotBeNull();
        var addressClaim = introspectionResponse.Json.Value.TryGetString("address");
        addressClaim.ShouldBe("{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task jwt_response_type_should_handle_complex_claim_in_introspection_claim()
    {
        var tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Scope = "api1 offline_access roles",
            UserName = "bob",
            Password = "bob"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = Constants.TokenTypeHints.AccessToken,
            ResponseFormat = ResponseFormat.Jwt
        }, _ct);

        introspectionResponse.Json.ShouldNotBeNull();
        var rolesClaim = introspectionResponse.Json.Value.TryGetStringArray("role").ToList();
        rolesClaim.ShouldNotBeNull();
        rolesClaim.Count.ShouldBe(2);
        rolesClaim.ShouldContain("Admin");
        rolesClaim.ShouldContain("Geek");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_active_token_should_increment_telemetry()
    {
        using var listener = StartListeningForMeasurements(Telemetry.Metrics.Counters.Introspection, out var measurements);
        var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "api1"
        }, _ct);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(true);
        introspectionResponse.IsError.ShouldBe(false);
        measurements.Count.ShouldBe(1);
        measurements[0].Name.ShouldBe(Telemetry.Metrics.Counters.Introspection);
        measurements[0].Value.ShouldBe(1);
        measurements[0].Tags.ShouldContain(new KeyValuePair<string, object>("active", true));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task token_that_fails_validation_should_increment_telemetry()
    {
        using var listener = StartListeningForMeasurements(Telemetry.Metrics.Counters.Introspection, out var measurements);

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Token = "invalid"
        }, _ct);

        introspectionResponse.IsActive.ShouldBe(false);
        introspectionResponse.IsError.ShouldBe(false);
        measurements.Count.ShouldBe(1);
        measurements[0].Name.ShouldBe(Telemetry.Metrics.Counters.Introspection);
        measurements[0].Value.ShouldBe(1);
        measurements[0].Tags.ShouldContain(new KeyValuePair<string, object>("active", false));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task token_that_results_in_validation_error_should_increment_telemetry()
    {
        using var listener = StartListeningForMeasurements(Telemetry.Metrics.Counters.Introspection, out var measurements);

        var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", "api1" },
            { "client_secret", "secret" },
            { "token", "" }
        });
        var rawIntrospectionResponse = await _client.PostAsync(IntrospectionEndpoint, requestContent, _ct);
        var introspectionResponse = await rawIntrospectionResponse.Content.ReadFromJsonAsync<TokenIntrospectionResponse>(_ct);

        introspectionResponse.IsActive.ShouldBe(false);
        introspectionResponse.IsError.ShouldBe(false);
        measurements.Count.ShouldBe(1);
        measurements[0].Name.ShouldBe(Telemetry.Metrics.Counters.Introspection);
        measurements[0].Value.ShouldBe(1);
        measurements[0].Tags.ShouldContain(new KeyValuePair<string, object>("error", "missing_token"));
    }

    private Dictionary<string, JsonElement> GetFields(TokenIntrospectionResponse response) => response.Raw.GetFields();

    private static MeterListener StartListeningForMeasurements(string meterName,
        out List<(string Name, long Value, KeyValuePair<string, object>[] Tags)> results)
    {
        var listener = new MeterListener();
        List<(string Name, long Value, KeyValuePair<string, object>[] Tags)> measurements = new();

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            measurements.Add((instrument.Name, measurement, tags.ToArray()));
        });

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == meterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        results = measurements;
        return listener;
    }
}
