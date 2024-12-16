// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using FluentAssertions;
using IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace IntegrationTests.Clients;

public class ResourceOwnerClient
{
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public ResourceOwnerClient()
    {
        var builder = new WebHostBuilder()
            .UseStartup<Startup>();
        var server = new TestServer(builder);

        _client = server.CreateClient();
    }

    [Fact]
    public async Task Valid_user_should_succeed_with_expected_response_payload()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "api1",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().Be(false);
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().BeNull();

        var payload = GetPayload(response);

        payload.Count.Should().Be(12);
        payload["iss"].GetString().Should().Be("https://idsvr4");
        payload["aud"].GetString().Should().Be("api");
        payload["client_id"].GetString().Should().Be("roclient");
        payload["sub"].GetString().Should().Be("88421113");
        payload["idp"].GetString().Should().Be("local");
        payload.Keys.Should().Contain("jti");
        payload.Keys.Should().Contain("iat");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.Should().Be(1);
        scopes.Should().Contain("api1");

        var amr = payload["amr"].EnumerateArray().ToList();
        amr.Count.Should().Be(1);
        amr.First().GetString().Should().Be("pwd");
    }

    [Fact]
    public async Task Request_with_no_explicit_scopes_should_return_allowed_scopes()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().Be(false);
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        var payload = GetPayload(response);

        payload["iss"].GetString().Should().Be("https://idsvr4");
        payload["aud"].GetString().Should().Be("api");
        payload["client_id"].GetString().Should().Be("roclient");
        payload["sub"].GetString().Should().Be("88421113");
        payload["idp"].GetString().Should().Be("local");
        payload.Keys.Should().Contain("jti");
        payload.Keys.Should().Contain("iat");

        var amr = payload["amr"].EnumerateArray().ToList();
        amr.Count.Should().Be(1);
        amr.First().GetString().Should().Be("pwd");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.Should().Be(8);

        scopes.Should().Contain("address");
        scopes.Should().Contain("api1");
        scopes.Should().Contain("api2");
        scopes.Should().Contain("api4.with.roles");
        scopes.Should().Contain("email");
        scopes.Should().Contain("offline_access");
        scopes.Should().Contain("openid");
        scopes.Should().Contain("roles");
    }

    [Fact]
    public async Task Request_containing_identity_scopes_should_return_expected_payload()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid email api1",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().Be(false);
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().BeNull();

        var payload = GetPayload(response);

        payload.Count.Should().Be(12);
        payload["iss"].GetString().Should().Be("https://idsvr4");
        payload["aud"].GetString().Should().Be("api");
        payload["client_id"].GetString().Should().Be("roclient");
        payload["sub"].GetString().Should().Be("88421113");
        payload["idp"].GetString().Should().Be("local");
        payload.Keys.Should().Contain("jti");
        payload.Keys.Should().Contain("iat");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().Should().Be(1);
        amr.First().ToString().Should().Be("pwd");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.Should().Be(3);
        scopes.Should().Contain("api1");
        scopes.Should().Contain("email");
        scopes.Should().Contain("openid");
    }

    [Fact]
    public async Task Request_for_refresh_token_should_return_expected_payload()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid email api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().Be(false);
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var payload = GetPayload(response);

        payload.Count.Should().Be(12);
        payload["iss"].GetString().Should().Be("https://idsvr4");
        payload["aud"].GetString().Should().Be("api");
        payload["client_id"].GetString().Should().Be("roclient");
        payload["sub"].GetString().Should().Be("88421113");
        payload["idp"].GetString().Should().Be("local");
        payload.Keys.Should().Contain("jti");
        payload.Keys.Should().Contain("iat");

        var amr = payload["amr"].EnumerateArray().ToList();
        amr.Count.Should().Be(1);
        amr.First().ToString().Should().Be("pwd");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.Should().Be(4);
        scopes.Should().Contain("api1");
        scopes.Should().Contain("email");
        scopes.Should().Contain("offline_access");
        scopes.Should().Contain("openid");
    }

    [Fact]
    public async Task Unknown_user_should_fail()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "api1",
            UserName = "unknown",
            Password = "bob"
        });

        response.IsError.Should().Be(true);
        response.ErrorType.Should().Be(ResponseErrorType.Protocol);
        response.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task User_with_empty_password_should_succeed()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "api1",
            UserName = "bob_no_password"
        });

        response.IsError.Should().Be(false);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task User_with_invalid_password_should_fail(string password)
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "api1",
            UserName = "bob",
            Password = password
        });

        response.IsError.Should().Be(true);
        response.ErrorType.Should().Be(ResponseErrorType.Protocol);
        response.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Error.Should().Be("invalid_grant");
    }


    private static Dictionary<string, JsonElement> GetPayload(TokenResponse response)
    {
        var token = response.AccessToken.Split('.').Skip(1).Take(1).First();
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Encoding.UTF8.GetString(Base64Url.Decode(token)));

        return dictionary;
    }
}
