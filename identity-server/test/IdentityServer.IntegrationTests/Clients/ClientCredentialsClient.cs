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

public class ClientCredentialsClient
{
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public ClientCredentialsClient()
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
    public async Task Invalid_endpoint_should_return_404()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint + "invalid",
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.Error.ShouldBe("Not Found");
        response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Valid_request_single_audience_should_return_expected_payload()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1"
        });

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(8);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");
    }

    [Fact]
    public async Task Valid_request_multiple_audiences_should_return_expected_payload()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1 other_api"
        });

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(8);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["client_id"].GetString().ShouldBe("client");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var audiences = payload["aud"].EnumerateArray().Select(x => x.ToString()).ToList();
        audiences.Count.ShouldBe(2);
        audiences.ShouldContain("api");
        audiences.ShouldContain("other_api");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");
    }

    [Fact]
    public async Task Valid_request_with_confirmation_should_return_expected_payload()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.cnf",
            ClientSecret = "foo",
            Scope = "api1"
        });

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(9);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client.cnf");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var cnf = payload["cnf"];
        cnf.TryGetString("x5t#S256").ShouldBe("foo");
    }

    [Fact]
    public async Task Requesting_multiple_scopes_should_return_expected_payload()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1 api2"
        });

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(8);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var scopes = payload["scope"].EnumerateArray().ToList();
        scopes.Count.ShouldBe(2);
        scopes.First().ToString().ShouldBe("api1");
        scopes.Skip(1).First().ToString().ShouldBe("api2");
    }

    [Fact]
    public async Task Request_with_no_explicit_scopes_should_return_expected_payload()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret"
        });

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload.Count.ShouldBe(8);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["client_id"].GetString().ShouldBe("client");
        payload.Keys.ShouldContain("jti");
        payload.Keys.ShouldContain("iat");

        var audiences = payload["aud"].EnumerateArray().Select(x => x.GetString()).ToList();
        audiences.Count.ShouldBe(2);
        audiences.ShouldContain("api");
        audiences.ShouldContain("other_api");

        var scopes = payload["scope"].EnumerateArray().Select(x => x.GetString()).ToList();
        scopes.Count.ShouldBe(3);
        scopes.ShouldContain("api1");
        scopes.ShouldContain("api2");
        scopes.ShouldContain("other_api");
    }

    [Fact]
    public async Task Client_without_default_scopes_skipping_scope_parameter_should_return_error()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.no_default_scopes",
            ClientSecret = "secret"
        });

        response.IsError.ShouldBe(true);
        response.ExpiresIn.ShouldBe(0);
        response.TokenType.ShouldBeNull();
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();
        response.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    public async Task Request_posting_client_secret_in_body_should_succeed()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1",

            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();

        var payload = GetPayload(response);

        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["aud"].GetString().ShouldBe("api");
        payload["client_id"].GetString().ShouldBe("client");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");
    }


    [Fact]
    public async Task Request_For_client_with_no_secret_and_basic_authentication_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.no_secret",
            Scope = "api1"
        });

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_client");
    }

    [Fact]
    public async Task Request_with_invalid_client_secret_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "invalid",
            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.Error.ShouldBe("invalid_client");
    }

    [Fact]
    public async Task Unknown_client_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "invalid",
            ClientSecret = "secret",
            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_client");
    }

    [Fact]
    public async Task Implicit_client_should_not_use_client_credential_grant()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "implicit",
            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("unauthorized_client");
    }

    [Fact]
    public async Task Implicit_and_client_creds_client_should_not_use_client_credential_grant_without_secret()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "implicit_and_client_creds",
            ClientSecret = "invalid",
            Scope = "api1"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_client");
    }


    [Fact]
    public async Task Requesting_unknown_scope_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "unknown"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_scope");
    }

    [Fact]
    public async Task Client_explicitly_requesting_identity_scope_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.identityscopes",
            ClientSecret = "secret",
            Scope = "openid api1"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_scope");
    }

    [Fact]
    public async Task Client_explicitly_requesting_offline_access_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1 offline_access"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_scope");
    }

    [Fact]
    public async Task Requesting_unauthorized_scope_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api3"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_scope");
    }

    [Fact]
    public async Task Requesting_authorized_and_unauthorized_scopes_should_fail()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1 api3"
        });

        response.IsError.ShouldBe(true);
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_scope");
    }

    private Dictionary<string, JsonElement> GetPayload(TokenResponse response)
    {
        var token = response.AccessToken.Split('.').Skip(1).Take(1).First();
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Encoding.UTF8.GetString(Base64Url.Decode(token)));

        return dictionary;
    }
}
