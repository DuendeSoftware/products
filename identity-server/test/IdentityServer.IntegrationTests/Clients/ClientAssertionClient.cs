// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class ClientAssertionClient
{
    private const string TokenEndpoint = "https://idsvr4/connect/token";
    private const string ClientId = "certificate_base64_valid";

    private readonly HttpClient _client;

    public ClientAssertionClient()
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
    public async Task Valid_client_with_manual_payload_should_succeed()
    {
        var token = CreateToken(ClientId);
        var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer" },
            { "client_assertion", token },
            { "grant_type", "client_credentials" },
            { "scope", "api1" }
        });

        var response = await GetToken(requestBody);

        AssertValidToken(response);
    }

    [Fact]
    public async Task Valid_client_should_succeed()
    {
        var token = CreateToken(ClientId);

        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            ClientId = ClientId,
            ClientAssertion =
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = token
            },

            Scope = "api1"
        });

        AssertValidToken(response);
    }

    [Fact]
    public async Task Valid_client_with_implicit_clientId_should_succeed()
    {
        var token = CreateToken(ClientId);

        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            ClientAssertion =
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = token
            },

            Scope = "api1"
        });

        AssertValidToken(response);
    }

    [Fact]
    public async Task Valid_client_with_token_replay_should_fail()
    {
        var token = CreateToken(ClientId);

        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            ClientId = ClientId,
            ClientAssertion =
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = token
            },

            Scope = "api1"
        });

        AssertValidToken(response);

        // replay
        response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            ClientId = ClientId,
            ClientAssertion =
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = token
            },

            Scope = "api1"
        });

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_client");
    }

    [Fact]
    public async Task Client_with_invalid_secret_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            ClientId = ClientId,
            ClientAssertion =
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = "invalid"
            },

            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.Error.ShouldBe(OidcConstants.TokenErrors.InvalidClient);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
    }

    [Fact]
    public async Task Invalid_client_should_fail()
    {
        const string clientId = "certificate_base64_invalid";
        var token = CreateToken(clientId);

        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            ClientId = clientId,
            ClientAssertion =
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = token
            },

            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.Error.ShouldBe(OidcConstants.TokenErrors.InvalidClient);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
    }

    private async Task<TokenResponse> GetToken(FormUrlEncodedContent body)
    {
        var response = await _client.PostAsync(TokenEndpoint, body);
        return await ProtocolResponse.FromHttpResponseAsync<TokenResponse>(response);
    }

    private void AssertValidToken(TokenResponse response)
    {
        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(8);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe(ClientId);
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

    private string CreateToken(string clientId, DateTime? nowOverride = null)
    {
        var certificate = TestCert.Load();
        var now = nowOverride ?? DateTime.UtcNow;

        var token = new JwtSecurityToken(
            clientId,
            TokenEndpoint,
            new List<Claim>()
            {
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.Subject, clientId),
                new Claim(JwtClaimTypes.IssuedAt, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            },
            now,
            now.AddMinutes(1),
            new SigningCredentials(
                new X509SecurityKey(certificate),
                SecurityAlgorithms.RsaSha256
            )
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }
}
