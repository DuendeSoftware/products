// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer;
using FluentAssertions;
using Duende.IdentityModel.Client;
using IntegrationTests.Endpoints.Introspection.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace IntegrationTests.Endpoints.Introspection;

public class IntrospectionTests
{
    private const string Category = "Introspection endpoint";
    private const string IntrospectionEndpoint = "https://server/connect/introspect";
    private const string TokenEndpoint = "https://server/connect/token";
    private const string RevocationEndpoint = "https://server/connect/revocation";

    private readonly HttpClient _client;
    private readonly HttpMessageHandler _handler;

    public IntrospectionTests()
    {
        var builder = new WebHostBuilder()
            .UseStartup<Startup>();
        var server = new TestServer(builder);

        _handler = server.CreateHandler();
        _client = server.CreateClient();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Empty_request_should_fail()
    {
        var form = new Dictionary<string, string>();

        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_scope_should_fail()
    {
        var form = new Dictionary<string, string>();

        _client.SetBasicAuthentication("unknown", "invalid");
        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_scope_secret_should_fail()
    {
        var form = new Dictionary<string, string>();

        _client.SetBasicAuthentication("api1", "invalid");
        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_token_should_fail()
    {
        var form = new Dictionary<string, string>();

        _client.SetBasicAuthentication("api1", "secret");
        var response = await _client.PostAsync(IntrospectionEndpoint, new FormUrlEncodedContent(form));

        response.Should().Be400BadRequest();
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
        });

        introspectionResponse.IsActive.Should().Be(false);
        introspectionResponse.IsError.Should().Be(false);
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
        });

        var data = new
        {
            client_id = "api1",
            client_secret = "secret",
            token = tokenResponse.AccessToken
        };
        var json = JsonSerializer.Serialize(data);

        var client = new HttpClient(_handler);
        var response = await client.PostAsync(IntrospectionEndpoint, new StringContent(json, Encoding.UTF8, "application/json"));
        response.Should().Be415UnsupportedMediaType();
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = "invalid"
        });

        introspectionResponse.IsActive.Should().Be(true);
        introspectionResponse.IsError.Should().Be(false);

        var scopes = from c in introspectionResponse.Claims
            where c.Type == "scope"
            select c;

        scopes.Count().Should().Be(1);
        scopes.First().Value.Should().Be("api1");
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
        TokenResponse tokenResponse;

        tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = introspectedBy,
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken,
            TokenTypeHint = hint
        });

        introspectionResponse.IsActive.Should().Be(true);
        introspectionResponse.IsError.Should().Be(false);

        var scopes = from c in introspectionResponse.Claims
                        where c.Type == "scope"
                        select c.Value;
        scopes.Should().Contain("api1");
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

    public async Task Refresh_tokens_can_be_introspected_by_their_client_with_any_hint(string introspectedBy, string hint, bool isActive)
    {
        TokenResponse tokenResponse;

        tokenResponse = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1 offline_access"
        });
     
        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = introspectedBy,
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken,
            TokenTypeHint = hint
        });

        if (isActive)
        {
            introspectionResponse.IsActive.Should().Be(true);
            introspectionResponse.IsError.Should().Be(false);

            var scopes = from c in introspectionResponse.Claims
                         where c.Type == "scope"
                         select c;

            scopes.Count().Should().Be(2);
            scopes.First().Value.Should().Be("api1");
        }
        else
        {
            introspectionResponse.IsActive.Should().Be(false);
            introspectionResponse.IsError.Should().Be(false);
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsActive.Should().Be(true);
        introspectionResponse.IsError.Should().Be(false);

        var scopes = from c in introspectionResponse.Claims
            where c.Type == "scope"
            select c;

        scopes.Count().Should().Be(1);
        scopes.First().Value.Should().Be("api1");
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        var values = GetFields(introspectionResponse);
            
        values["iss"].ValueKind.Should().Be(JsonValueKind.String);
        values["aud"].ValueKind.Should().Be(JsonValueKind.String);
        values["nbf"].ValueKind.Should().Be(JsonValueKind.Number);
        values["exp"].ValueKind.Should().Be(JsonValueKind.Number);
        values["client_id"].ValueKind.Should().Be(JsonValueKind.String);
        values["active"].ValueKind.Should().Be(JsonValueKind.True);
        values["scope"].ValueKind.Should().Be(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().Should().Be("api1");
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
        });

        tokenResponse.IsError.Should().BeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        var values = GetFields(introspectionResponse);
            
        values["iss"].ValueKind.Should().Be(JsonValueKind.String);
        values["aud"].ValueKind.Should().Be(JsonValueKind.String);
        values["nbf"].ValueKind.Should().Be(JsonValueKind.Number);
        values["exp"].ValueKind.Should().Be(JsonValueKind.Number);
        values["client_id"].ValueKind.Should().Be(JsonValueKind.String);
        values["active"].ValueKind.Should().Be(JsonValueKind.True);
        values["scope"].ValueKind.Should().Be(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().Should().Be("api1");
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
        });

        tokenResponse.IsError.Should().BeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api3",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        var values = GetFields(introspectionResponse);

        values["aud"].ValueKind.Should().Be(JsonValueKind.Array);

        var audiences = values["aud"].EnumerateArray();
        foreach (var aud in audiences)
        {
            aud.ValueKind.Should().Be(JsonValueKind.String);
        }

        values["iss"].ValueKind.Should().Be(JsonValueKind.String);
        values["nbf"].ValueKind.Should().Be(JsonValueKind.Number);
        values["exp"].ValueKind.Should().Be(JsonValueKind.Number);
        values["client_id"].ValueKind.Should().Be(JsonValueKind.String);
        values["active"].ValueKind.Should().Be(JsonValueKind.True);
        values["scope"].ValueKind.Should().Be(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().Should().Be("api3-a api3-b");
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
        });

        tokenResponse.IsError.Should().BeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api3",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        var values = GetFields(introspectionResponse);

        values["iss"].ValueKind.Should().Be(JsonValueKind.String);
        values["aud"].ValueKind.Should().Be(JsonValueKind.String);
        values["nbf"].ValueKind.Should().Be(JsonValueKind.Number);
        values["exp"].ValueKind.Should().Be(JsonValueKind.Number);
        values["client_id"].ValueKind.Should().Be(JsonValueKind.String);
        values["active"].ValueKind.Should().Be(JsonValueKind.True);
        values["scope"].ValueKind.Should().Be(JsonValueKind.String);

        var scopes = values["scope"];
        scopes.GetString().Should().Be("api3-a api3-b");
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
        });

        tokenResponse.IsError.Should().BeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api3",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsActive.Should().BeTrue();
        introspectionResponse.IsError.Should().BeFalse();

        var scopes = from c in introspectionResponse.Claims
            where c.Type == "scope"
            select c.Value;

        scopes.Count().Should().Be(1);
        scopes.First().Should().Be("api3-a");
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsActive.Should().Be(true);
        introspectionResponse.IsError.Should().Be(false);

        var scopes = from c in introspectionResponse.Claims
            where c.Type == "scope"
            select c;

        scopes.Count().Should().Be(1);
        scopes.First().Value.Should().Be("api1");
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api2",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsActive.Should().Be(false);
        introspectionResponse.IsError.Should().Be(false);
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsActive.Should().BeTrue();
        introspectionResponse.IsError.Should().BeFalse();
        introspectionResponse.Claims.Single(x => x.Type == "client_id").Value.Should().Be("client1");
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        });

        introspectionResponse.IsActive.Should().BeTrue();
        introspectionResponse.IsError.Should().BeFalse();
        introspectionResponse.Claims.Single(x => x.Type == "client_id").Value.Should().Be("ro.client");
        introspectionResponse.Claims.Single(x => x.Type == "sub").Value.Should().Be("1");
        introspectionResponse.Claims.Where(x => x.Type == "scope").Select(x => x.Value).Should().BeEquivalentTo(new[] { "api1", "offline_access" });
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        });

        introspectionResponse.IsActive.Should().BeFalse();
        introspectionResponse.IsError.Should().BeFalse();
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
        });

        var revocationResponse = await _client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = RevocationEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        });
        revocationResponse.IsError.Should().BeFalse();

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        });

        introspectionResponse.IsActive.Should().BeFalse();
        introspectionResponse.IsError.Should().BeFalse();
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "client2",
            ClientSecret = "secret",

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsActive.Should().BeFalse();
        introspectionResponse.IsError.Should().BeFalse();
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
        });

        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "ro.client2",
            ClientSecret = "secret",

            Token = tokenResponse.RefreshToken
        });

        introspectionResponse.IsActive.Should().BeFalse();
        introspectionResponse.IsError.Should().BeFalse();
    }

    private Dictionary<string, JsonElement> GetFields(TokenIntrospectionResponse response) => response.Raw.GetFields();
}