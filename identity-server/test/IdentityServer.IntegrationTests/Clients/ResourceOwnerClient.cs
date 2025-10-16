// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class ResourceOwnerClient
{
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public ResourceOwnerClient()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseStartup<Startup>();
            });

        var host = hostBuilder.Start();

        _client = host.GetTestClient();
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

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("roclient");
        payload["sub"].GetString().ShouldBe("88421113");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.ShouldBe(1);
        scopes.ShouldContain("api1");

        var amr = payload["amr"].EnumerateArray().ToList();
        amr.Count.ShouldBe(1);
        amr.First().GetString().ShouldBe("pwd");
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

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldNotBeNull();

        var payload = GetPayload(response);

        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("roclient");
        payload["sub"].GetString().ShouldBe("88421113");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var amr = payload["amr"].EnumerateArray().ToList();
        amr.Count.ShouldBe(1);
        amr.First().GetString().ShouldBe("pwd");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.ShouldBe(8);

        scopes.ShouldContain("address");
        scopes.ShouldContain("api1");
        scopes.ShouldContain("api2");
        scopes.ShouldContain("api4.with.roles");
        scopes.ShouldContain("email");
        scopes.ShouldContain("offline_access");
        scopes.ShouldContain("openid");
        scopes.ShouldContain("roles");
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

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("roclient");
        payload["sub"].GetString().ShouldBe("88421113");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("pwd");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.ShouldBe(3);
        scopes.ShouldContain("api1");
        scopes.ShouldContain("email");
        scopes.ShouldContain("openid");
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

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("roclient");
        payload["sub"].GetString().ShouldBe("88421113");
        payload["idp"].GetString().ShouldBe("local");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var amr = payload["amr"].EnumerateArray().ToList();
        amr.Count.ShouldBe(1);
        amr.First().ToString().ShouldBe("pwd");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.ToString()).ToList();
        scopes.Count.ShouldBe(4);
        scopes.ShouldContain("api1");
        scopes.ShouldContain("email");
        scopes.ShouldContain("offline_access");
        scopes.ShouldContain("openid");
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

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_grant");
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

        response.IsError.ShouldBe(false);
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

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_grant");
    }


    private static Dictionary<string, JsonElement> GetPayload(TokenResponse response)
    {
        var token = response.AccessToken.Split('.').Skip(1).Take(1).First();
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Encoding.UTF8.GetString(Base64Url.Decode(token)));

        return dictionary;
    }
}
