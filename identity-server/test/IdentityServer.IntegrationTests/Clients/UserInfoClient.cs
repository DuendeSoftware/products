// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class UserInfoEndpointClient
{
    private const string TokenEndpoint = "https://server/connect/token";
    private const string UserInfoEndpoint = "https://server/connect/userinfo";

    private readonly HttpClient _client;

    public UserInfoEndpointClient()
    {
        var builder = new WebHostBuilder()
            .UseStartup<Startup>();
        var server = new TestServer(builder);

        _client = server.CreateClient();
    }

    [Fact]
    public async Task Valid_client_with_GET_should_succeed()
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

        response.IsError.ShouldBeFalse();

        var userInfo = await _client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = UserInfoEndpoint,
            Token = response.AccessToken
        });

        userInfo.IsError.ShouldBeFalse();
        userInfo.Claims.Count().ShouldBe(3);

        userInfo.Claims.ShouldContain(c => c.Type == "sub" && c.Value == "88421113");
        userInfo.Claims.ShouldContain(c => c.Type == "email" && c.Value == "BobSmith@example.com");
        userInfo.Claims.ShouldContain(c => c.Type == "email_verified" && c.Value == "true");
    }

    [Fact]
    public async Task Request_address_scope_should_return_expected_response()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid address",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.ShouldBeFalse();

        var userInfo = await _client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = UserInfoEndpoint,
            Token = response.AccessToken
        });

        userInfo.IsError.ShouldBeFalse();
        userInfo.Claims.First().Value.ShouldBe("{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }");
    }

    [Fact]
    public async Task Using_a_token_with_no_identity_scope_should_fail()
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

        response.IsError.ShouldBeFalse();

        var userInfo = await _client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = UserInfoEndpoint,
            Token = response.AccessToken
        });

        userInfo.IsError.ShouldBeTrue();
        userInfo.HttpStatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Using_a_token_with_an_identity_scope_but_no_openid_should_fail()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "email api1",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.ShouldBeFalse();

        var userInfo = await _client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = UserInfoEndpoint,
            Token = response.AccessToken
        });

        userInfo.IsError.ShouldBeTrue();
        userInfo.HttpStatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_token_should_fail()
    {
        var userInfo = await _client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = UserInfoEndpoint,
            Token = "invalid"
        });

        userInfo.IsError.ShouldBeTrue();
        userInfo.HttpStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Complex_json_should_be_correct()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid email api1 api4.with.roles roles",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.ShouldBeFalse();

        var payload = GetPayload(response);

        var scopes = ((JsonElement)payload["scope"]).ToStringList();
        scopes.Count.ShouldBe(5);
        scopes.ShouldContain("openid");
        scopes.ShouldContain("email");
        scopes.ShouldContain("api1");
        scopes.ShouldContain("api4.with.roles");
        scopes.ShouldContain("roles");

        var roles = ((JsonElement)payload["role"]).ToStringList();
        roles.Count.ShouldBe(2);
        roles.ShouldContain("Geek");
        roles.ShouldContain("Developer");

        var userInfo = await _client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = UserInfoEndpoint,
            Token = response.AccessToken
        });

        roles = userInfo.Json?.TryGetStringArray("role").ToList();
        roles.Count.ShouldBe(2);
        roles.ShouldContain("Geek");
        roles.ShouldContain("Developer");
    }

    private Dictionary<string, object> GetPayload(TokenResponse response)
    {
        var token = response.AccessToken.Split('.').Skip(1).Take(1).First();
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(
            Encoding.UTF8.GetString(Base64Url.Decode(token)));

        return dictionary;
    }
}
