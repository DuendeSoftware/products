// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Net.Http.Json;
using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using IntegrationTests.TestHosts;

namespace IntegrationTests;

public class DynamicClientRegistrationValidationTests : ConfigurationIntegrationTestBase
{
    [Fact]
    public async Task http_get_method_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.GetAsync("/connect/dcr");
        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task incorrect_content_type_should_fail()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            { "redirect_uris", "https://example.com/callback" },
            { "grant_types", "authorization_code" }
        });
        var response = await ConfigurationHost.HttpClient!.PostAsync("/connect/dcr", content);
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task missing_grant_type_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", new
        {
            redirect_uris = new[] { "https://example.com/callback" }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationError>();
        error?.Error.ShouldBe("invalid_client_metadata");
    }

    [Fact]
    public async Task unsupported_grant_type_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", new
        {
            redirect_uris = new[] { "https://example.com/callback" },
            grant_types = new[] { "password" }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationError>();
        error?.Error.ShouldBe("invalid_client_metadata");
    }

    [Fact]
    public async Task client_credentials_with_redirect_uri_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", new
        {
            redirect_uris = new[] { "https://example.com/callback" },
            grant_types = new[] { "client_credentials" }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationError>();
        error?.Error.ShouldBe("invalid_redirect_uri");
    }

    [Fact]
    public async Task auth_code_without_redirect_uri_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", new
        {
            grant_types = new[] { "authorization_code", "client_credentials" }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationError>();
        error?.Error.ShouldBe("invalid_redirect_uri");
    }

    [Fact]
    public async Task client_credentials_and_refresh_token_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", new
        {
            grant_types = new[] { "client_credentials", "refresh_token" }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationError>();
        error?.Error.ShouldBe("invalid_client_metadata");
    }

    [Fact]
    public async Task jwks_and_jwks_uri_used_together_should_fail()
    {
        var response = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr",
            new DynamicClientRegistrationRequest
            {
                GrantTypes = { "client_credentials" },
                Jwks = new KeySet(Array.Empty<string>()),
                JwksUri = new Uri("https://example.com")
            }
        );
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationError>();
        error?.Error.ShouldBe("invalid_client_metadata");
    }
}
