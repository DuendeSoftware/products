// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class CustomTokenResponseClients
{
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public CustomTokenResponseClients()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseStartup<StartupWithCustomTokenResponses>();
            });

        var host = hostBuilder.Start();

        _client = host.GetTestClient();
    }

    [Fact]
    public async Task Resource_owner_success_should_return_custom_response()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            UserName = "bob",
            Password = "bob",
            Scope = "api1"
        });

        // raw fields
        var fields = GetFields(response);
        fields["string_value"].GetString().ShouldBe("some_string");
        fields["int_value"].GetInt32().ShouldBe(42);

        JsonElement temp;
        fields.TryGetValue("identity_token", out temp).ShouldBeFalse();
        fields.TryGetValue("refresh_token", out temp).ShouldBeFalse();
        fields.TryGetValue("error", out temp).ShouldBeFalse();
        fields.TryGetValue("error_description", out temp).ShouldBeFalse();
        fields.TryGetValue("token_type", out temp).ShouldBeTrue();
        fields.TryGetValue("expires_in", out temp).ShouldBeTrue();

        var responseObject = fields["dto"];

        var responseDto = GetDto(responseObject);
        var dto = CustomResponseDto.Create;

        responseDto.string_value.ShouldBe(dto.string_value);
        responseDto.int_value.ShouldBe(dto.int_value);
        responseDto.nested.string_value.ShouldBe(dto.nested.string_value);
        responseDto.nested.int_value.ShouldBe(dto.nested.int_value);


        // token client response
        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();


        // token content
        var payload = GetPayload(response);
        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["client_id"].GetString().ShouldBe("roclient");
        payload["sub"].GetString().ShouldBe("bob");
        payload["idp"].GetString().ShouldBe("local");
        payload["aud"].GetString().ShouldBe("api");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("password");
    }

    [Fact]
    public async Task Resource_owner_failure_should_return_custom_error_response()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            UserName = "bob",
            Password = "invalid",
            Scope = "api1"
        });

        // raw fields
        var fields = GetFields(response);
        fields["string_value"].GetString().ShouldBe("some_string");
        fields["int_value"].GetInt32().ShouldBe(42);

        JsonElement temp;
        fields.TryGetValue("identity_token", out temp).ShouldBeFalse();
        fields.TryGetValue("refresh_token", out temp).ShouldBeFalse();
        fields.TryGetValue("error", out temp).ShouldBeTrue();
        fields.TryGetValue("error_description", out temp).ShouldBeTrue();
        fields.TryGetValue("token_type", out temp).ShouldBeFalse();
        fields.TryGetValue("expires_in", out temp).ShouldBeFalse();

        var responseObject = fields["dto"];

        var responseDto = GetDto(responseObject);
        var dto = CustomResponseDto.Create;

        responseDto.string_value.ShouldBe(dto.string_value);
        responseDto.int_value.ShouldBe(dto.int_value);
        responseDto.nested.string_value.ShouldBe(dto.nested.string_value);
        responseDto.nested.int_value.ShouldBe(dto.nested.int_value);


        // token client response
        response.IsError.ShouldBe(true);
        response.Error.ShouldBe("invalid_grant");
        response.ErrorDescription.ShouldBe("invalid_credential");
        response.ExpiresIn.ShouldBe(0);
        response.TokenType.ShouldBeNull();
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();
    }

    [Fact]
    public async Task Extension_grant_success_should_return_custom_response()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },
                { "outcome", "succeed"}
            }
        });


        // raw fields
        var fields = GetFields(response);
        fields["string_value"].GetString().ShouldBe("some_string");
        fields["int_value"].GetInt32().ShouldBe(42);

        JsonElement temp;
        fields.TryGetValue("identity_token", out temp).ShouldBeFalse();
        fields.TryGetValue("refresh_token", out temp).ShouldBeFalse();
        fields.TryGetValue("error", out temp).ShouldBeFalse();
        fields.TryGetValue("error_description", out temp).ShouldBeFalse();
        fields.TryGetValue("token_type", out temp).ShouldBeTrue();
        fields.TryGetValue("expires_in", out temp).ShouldBeTrue();

        var responseObject = fields["dto"];

        var responseDto = GetDto(responseObject);
        var dto = CustomResponseDto.Create;

        responseDto.string_value.ShouldBe(dto.string_value);
        responseDto.int_value.ShouldBe(dto.int_value);
        responseDto.nested.string_value.ShouldBe(dto.nested.string_value);
        responseDto.nested.int_value.ShouldBe(dto.nested.int_value);


        // token client response
        response.IsError.ShouldBe(false);
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();


        // token content
        var payload = GetPayload(response);
        payload.Count.ShouldBe(12);
        payload["iss"].GetString().ShouldBe("https://idsvr4");
        payload["client_id"].GetString().ShouldBe("client.custom");
        payload["sub"].GetString().ShouldBe("bob");
        payload["idp"].GetString().ShouldBe("local");
        payload["aud"].GetString().ShouldBe("api");

        var scopes = payload["scope"].EnumerateArray();
        scopes.First().ToString().ShouldBe("api1");

        var amr = payload["amr"].EnumerateArray();
        amr.Count().ShouldBe(1);
        amr.First().ToString().ShouldBe("custom");
    }

    [Fact]
    public async Task Extension_grant_failure_should_return_custom_error_response()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },
                { "outcome", "fail"}
            }
        });


        // raw fields
        var fields = GetFields(response);
        fields["string_value"].GetString().ShouldBe("some_string");
        fields["int_value"].GetInt32().ShouldBe(42);

        JsonElement temp;
        fields.TryGetValue("identity_token", out temp).ShouldBeFalse();
        fields.TryGetValue("refresh_token", out temp).ShouldBeFalse();
        fields.TryGetValue("error", out temp).ShouldBeTrue();
        fields.TryGetValue("error_description", out temp).ShouldBeTrue();
        fields.TryGetValue("token_type", out temp).ShouldBeFalse();
        fields.TryGetValue("expires_in", out temp).ShouldBeFalse();

        var responseObject = fields["dto"];

        var responseDto = GetDto(responseObject);
        var dto = CustomResponseDto.Create;

        responseDto.string_value.ShouldBe(dto.string_value);
        responseDto.int_value.ShouldBe(dto.int_value);
        responseDto.nested.string_value.ShouldBe(dto.nested.string_value);
        responseDto.nested.int_value.ShouldBe(dto.nested.int_value);


        // token client response
        response.IsError.ShouldBe(true);
        response.Error.ShouldBe("invalid_grant");
        response.ErrorDescription.ShouldBe("invalid_credential");
        response.ExpiresIn.ShouldBe(0);
        response.TokenType.ShouldBeNull();
        response.IdentityToken.ShouldBeNull();
        response.RefreshToken.ShouldBeNull();
    }

    private CustomResponseDto GetDto(JsonElement responseObject) => responseObject.ToObject<CustomResponseDto>();

    private Dictionary<string, JsonElement> GetFields(TokenResponse response) => response.Raw.GetFields();


    private Dictionary<string, JsonElement> GetPayload(TokenResponse response)
    {
        var token = response.AccessToken.Split('.').Skip(1).Take(1).First();
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token)));

        return dictionary;
    }
}
