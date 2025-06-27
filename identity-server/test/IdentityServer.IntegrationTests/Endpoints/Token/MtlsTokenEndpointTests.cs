// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using IntegrationTests.Common;

namespace IntegrationTests.Endpoints.Token;

public class MtlsTokenEndpointTests
{
    private const string Category = "mTLS Token endpoint";

    private IdentityServerPipeline _pipeline = new IdentityServerPipeline();

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_request_with_client_certificate_should_succeed()
    {
        // Arrange
        var clientId = "mtls_client";

        // Load a test certificate
        var clientCert = TestCert.Load();

        // Add a client that requires mTLS (client certificate)
        var client = new Client
        {
            ClientId = clientId,
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint,
                    Value = clientCert.Thumbprint
                }
            },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" }
        };

        _pipeline.Clients.Add(client);
        _pipeline.ApiScopes.Add(new ApiScope("scope1"));
        _pipeline.Initialize();

        // Set the client certificate in the pipeline
        _pipeline.SetClientCertificate(clientCert);

        // Act - Make a client credentials request using mTLS client
        var tokenClient = _pipeline.GetMtlsClient();

        var formParams = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "scope", "scope1" }
        };

        var form = new FormUrlEncodedContent(formParams);
        var response = await tokenClient.PostAsync("/connect/mtls/token", form);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("access_token");
        json.ShouldContain("\"token_type\":\"Bearer\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_request_without_client_certificate_should_still_work_with_secret()
    {
        // Arrange
        var clientId = "regular_client";
        var clientSecret = "secret";

        var client = new Client
        {
            ClientId = clientId,
            ClientSecrets = { new Secret(clientSecret.Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" }
        };

        _pipeline.Clients.Add(client);
        _pipeline.ApiScopes.Add(new ApiScope("scope1"));
        _pipeline.Initialize();

        // Act - Make a client credentials request using regular client (no mTLS)
        var tokenClient = _pipeline.BrowserClient;

        var formParams = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "scope", "scope1" }
        };

        var form = new FormUrlEncodedContent(formParams);
        var response = await tokenClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("access_token");
        json.ShouldContain("\"token_type\":\"Bearer\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task infrastructure_should_reject_mtls_client_request_when_no_certificate_set()
    {
        // Arrange
        _pipeline.Initialize();

        // Act & Assert - Trying to get mTLS client without setting certificate should throw
        var action = () => _pipeline.GetMtlsClient();
        action.ShouldThrow<InvalidOperationException>("No client certificate has been set. Call SetClientCertificate() first.");
    }
}
