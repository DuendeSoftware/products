// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Token;

public class MtlsClientCertificateAuthenticationTests
{
    private readonly CancellationToken _cancellationToken = CancellationToken.None;

    [Fact]
    public async Task when_certificate_from_untrusted_ca_for_client_registered_by_name_calls_token_endpoint_expect_invalid_client()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var untrustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        using var untrustedCertificate = TestCertificates.CreateIssued("CN=name-client", untrustedCa);
        await using var host = await StartHostAsync(CertificateTypes.Chained, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, untrustedCertificate, "/connect/token", "name-client");

        await ShouldBeInvalidClientAsync(response, "a certificate whose chain is not trusted must not authenticate a client at the regular token endpoint");
    }

    [Fact]
    public async Task when_certificate_from_untrusted_ca_for_api_resource_registered_by_name_calls_introspection_endpoint_expect_unauthorized()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var untrustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        using var untrustedCertificate = TestCertificates.CreateIssued("CN=name-api", untrustedCa);
        await using var host = await StartHostAsync(CertificateTypes.Chained, trustedCa, thumbprintCertificate);

        var response = await PostIntrospectionAsync(host, untrustedCertificate, "name-api");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a certificate whose chain is not trusted must not authenticate an API resource at the introspection endpoint");
    }
    [Fact]
    public async Task when_certificate_from_trusted_ca_for_client_registered_by_name_calls_mtls_token_endpoint_expect_access_token()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        using var clientCertificate = TestCertificates.CreateIssued("CN=name-client", trustedCa);
        await using var host = await StartHostAsync(CertificateTypes.Chained, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, clientCertificate, "/connect/mtls/token", "name-client");

        await ShouldHaveAccessTokenAsync(response, "a certificate issued by the trusted CA must authenticate the client at the mTLS token endpoint");
    }

    [Fact]
    public async Task when_certificate_from_trusted_ca_for_client_registered_by_name_calls_token_endpoint_expect_access_token()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        using var clientCertificate = TestCertificates.CreateIssued("CN=name-client", trustedCa);
        await using var host = await StartHostAsync(CertificateTypes.Chained, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, clientCertificate, "/connect/token", "name-client");

        await ShouldHaveAccessTokenAsync(response, "a certificate issued by the trusted CA must keep authenticating the client at the regular token endpoint");
    }

    [Fact]
    public async Task when_self_signed_certificate_for_client_registered_by_name_calls_mtls_token_endpoint_with_self_signed_allowed_expect_invalid_client()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        using var selfSignedCertificate = TestCertificates.CreateSelfSigned("CN=name-client");
        await using var host = await StartHostAsync(CertificateTypes.Chained | CertificateTypes.SelfSigned, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, selfSignedCertificate, "/connect/mtls/token", "name-client");

        await ShouldBeInvalidClientAsync(response, "a self-signed certificate must not satisfy a certificate name secret, even when the handler accepts self-signed certificates");
    }

    [Fact]
    public async Task when_client_registered_by_thumbprint_presents_its_own_self_signed_certificate_to_mtls_token_endpoint_expect_access_token()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        await using var host = await StartHostAsync(CertificateTypes.Chained | CertificateTypes.SelfSigned, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, thumbprintCertificate, "/connect/mtls/token", "thumbprint-client");

        await ShouldHaveAccessTokenAsync(response, "a self-signed certificate that matches the registered thumbprint must authenticate the client");
    }

    [Fact]
    public async Task when_client_registered_by_thumbprint_presents_its_own_self_signed_certificate_to_token_endpoint_with_only_chained_allowed_expect_invalid_client()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        await using var host = await StartHostAsync(CertificateTypes.Chained, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, thumbprintCertificate, "/connect/token", "thumbprint-client");

        await ShouldBeInvalidClientAsync(response, "a self-signed certificate must not authenticate a client at the token endpoint when the handler only allows chained certificates");
    }
    [Fact]
    public async Task when_client_with_shared_secret_calls_token_endpoint_while_presenting_a_self_signed_certificate_expect_access_token()
    {
        using var trustedCa = TestCertificates.CreateCa();
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=thumbprint-client");
        using var unrelatedCertificate = TestCertificates.CreateSelfSigned("CN=unrelated");
        await using var host = await StartHostAsync(CertificateTypes.Chained, trustedCa, thumbprintCertificate);

        var response = await PostClientCredentialsAsync(host, unrelatedCertificate, "/connect/token", "shared-secret-client", "shared-secret");

        await ShouldHaveAccessTokenAsync(response, "a client that authenticates with a shared secret must not be affected by an unrelated client certificate");
    }

    private async Task<KestrelTestHost> StartHostAsync(
        CertificateTypes allowedCertificateTypes,
        X509Certificate2 trustedCa,
        X509Certificate2 thumbprintCertificate) =>
        await KestrelTestHost.Create(
            services =>
            {
                services.Configure<KestrelServerOptions>(options => options.ConfigureHttpsDefaults(https =>
                {
                    https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                    https.AllowAnyClientCertificate();
                }));

                services.AddIdentityServer(options => options.MutualTls.Enabled = true)
                    .AddInMemoryApiScopes([new ApiScope("api")])
                    .AddInMemoryApiResources([new ApiResource("name-api") { Scopes = { "api" }, ApiSecrets = { new Secret("CN=name-api") { Type = IdentityServerConstants.SecretTypes.X509CertificateName } } }])
                    .AddInMemoryClients(
                    [
                        CreateClient("name-client", new Secret("CN=name-client") { Type = IdentityServerConstants.SecretTypes.X509CertificateName }),
                        CreateClient("thumbprint-client", new Secret(thumbprintCertificate.Thumbprint) { Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint }),
                        CreateClient("shared-secret-client", new Secret("shared-secret".Sha256()))
                    ])
                    .AddMutualTlsSecretValidators();

                services.AddAuthentication().AddCertificate(options =>
                {
                    options.AllowedCertificateTypes = allowedCertificateTypes;
                    options.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;
                    options.CustomTrustStore.Add(trustedCa);
                    options.RevocationMode = X509RevocationMode.NoCheck;
                });
            },
            app => app.UseIdentityServer(),
            _cancellationToken);

    private async Task<HttpResponseMessage> PostClientCredentialsAsync(
        KestrelTestHost host,
        X509Certificate2 clientCertificate,
        string endpoint,
        string clientId,
        string? clientSecret = null)
    {
        using var client = CreateHttpClient(host, clientCertificate);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["scope"] = "api"
        };
        if (clientSecret != null)
        {
            form["client_secret"] = clientSecret;
        }

        return await client.PostAsync(endpoint, new FormUrlEncodedContent(form), _cancellationToken);
    }

    private async Task<HttpResponseMessage> PostIntrospectionAsync(KestrelTestHost host, X509Certificate2 clientCertificate, string apiName)
    {
        using var client = CreateHttpClient(host, clientCertificate);

        var form = new Dictionary<string, string>
        {
            ["client_id"] = apiName,
            ["token"] = "not-a-token"
        };

        return await client.PostAsync("/connect/introspect", new FormUrlEncodedContent(form), _cancellationToken);
    }

    private static HttpClient CreateHttpClient(KestrelTestHost host, X509Certificate2 clientCertificate)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        handler.ClientCertificates.Add(clientCertificate);
        return new HttpClient(handler) { BaseAddress = new Uri(host.Uri()) };
    }
    private async Task ShouldBeInvalidClientAsync(HttpResponseMessage response, string because)
    {
        var body = await response.Content.ReadAsStringAsync(_cancellationToken);
        using var json = JsonDocument.Parse(body);
        var error = json.RootElement.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : null;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{because}, but got {(int) response.StatusCode}");
        error.ShouldBe("invalid_client", $"{because}, but the error was '{error}'");
    }

    private async Task ShouldHaveAccessTokenAsync(HttpResponseMessage response, string because)
    {
        var body = await response.Content.ReadAsStringAsync(_cancellationToken);
        using var json = JsonDocument.Parse(body);
        var hasAccessToken = json.RootElement.TryGetProperty("access_token", out _);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{because}, but got {(int) response.StatusCode}");
        hasAccessToken.ShouldBeTrue($"{because}, but the response has no access_token");
    }

    private static Client CreateClient(string clientId, Secret secret) => new()
    {
        ClientId = clientId,
        ClientSecrets = { secret },
        AllowedGrantTypes = GrantTypes.ClientCredentials,
        AllowedScopes = { "api" }
    };
}