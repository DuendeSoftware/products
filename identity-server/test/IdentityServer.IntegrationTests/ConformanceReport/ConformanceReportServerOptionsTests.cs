// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Cryptography;
using Duende.ConformanceReport.Endpoints;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.ConformanceReport;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.ConformanceReport;

/// <summary>
/// Verifies that the IdentityServer adapter passes IdentityServer configuration to the
/// conformance assessment, by checking the findings rendered in the report.
/// </summary>
public class ConformanceReportServerOptionsTests
{
    private static readonly Client NativeClient = new()
    {
        ClientId = "native",
        AllowedGrantTypes = GrantTypes.Code,
        RequireClientSecret = false,
        RedirectUris = { "http://127.0.0.1/callback" },
        AllowedScopes = { "openid" }
    };

    private const string LoopbackNotConfigured =
        "Redirect URI 'http://127.0.0.1/callback' is a loopback http redirect URI, but the server is not configured for loopback redirection.";

    [Fact]
    public async Task loopback_redirect_uris_are_not_enabled_by_default()
    {
        var report = await GetReportAsync(clients: [NativeClient]);

        report.ShouldContain(LoopbackNotConfigured);
    }

    [Fact]
    public async Task app_auth_redirect_uri_validator_enables_loopback_redirect_uris()
    {
        var report = await GetReportAsync(
            clients: [NativeClient],
            configureBuilder: builder => builder.AddAppAuthRedirectUriValidator());

        report.ShouldNotContain(LoopbackNotConfigured);
    }

    [Fact]
    public async Task token_signing_algorithms_come_from_signing_credentials()
    {
        using var rsa = RSA.Create();
        var report = await GetReportAsync(configureBuilder: builder =>
            builder.AddSigningCredential(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));

        report.ShouldContain("No FAPI 2.0 compliant algorithms are configured for signing issued tokens. Found: RS256.");
    }

    [Fact]
    public async Task fapi_compliant_signing_credential_passes_token_signing_algorithms()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var report = await GetReportAsync(configureBuilder: builder =>
            builder.AddSigningCredential(new ECDsaSecurityKey(ecdsa), SecurityAlgorithms.EcdsaSha256));

        report.ShouldContain("All algorithms configured for signing issued tokens are FAPI 2.0 compliant (PS256 or ES256).");
    }

    [Fact]
    public async Task token_signing_algorithms_come_from_automatic_key_management()
    {
        var report = await GetReportAsync(
            options =>
            {
                options.KeyManagement.Enabled = true;
                options.KeyManagement.SigningAlgorithms = [new SigningAlgorithmOptions(SecurityAlgorithms.EcdsaSha256)];
            },
            configureBuilder: builder => builder.Services.AddSingleton<ISigningKeyStore>(new InMemorySigningKeyStore()));

        report.ShouldContain("All algorithms configured for signing issued tokens are FAPI 2.0 compliant (PS256 or ES256).");
    }

    [Fact]
    public async Task automatic_key_management_reports_non_fapi_default_algorithm()
    {
        var report = await GetReportAsync(
            options => options.KeyManagement.Enabled = true,
            configureBuilder: builder => builder.Services.AddSingleton<ISigningKeyStore>(new InMemorySigningKeyStore()));

        report.ShouldContain("No FAPI 2.0 compliant algorithms are configured for signing issued tokens. Found: RS256.");
    }

    [Fact]
    public async Task mixed_signing_credentials_report_only_non_fapi_algorithms()
    {
        using var rsa = RSA.Create();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var report = await GetReportAsync(configureBuilder: builder => builder
            .AddSigningCredential(new ECDsaSecurityKey(ecdsa), SecurityAlgorithms.EcdsaSha256)
            .AddSigningCredential(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));

        report.ShouldContain("Non-FAPI 2.0 algorithms are configured for signing issued tokens: RS256.");
    }

    [Fact]
    public async Task no_signing_credentials_reports_empty_token_signing_algorithms()
    {
        var report = await GetReportAsync();

        report.ShouldContain("No signing algorithms are configured for issued tokens, so tokens are signed with the default RS256.");
    }

    [Fact]
    public async Task empty_dpop_algorithms_without_mutual_tls_fails()
    {
        var report = await GetReportAsync(options => options.DPoP.SupportedDPoPSigningAlgorithms = []);

        report.ShouldContain("No DPoP proof algorithms are configured, so all DPoP proofs are rejected, and mTLS is not enabled.");
    }

    [Fact]
    public async Task mutual_tls_enabled_is_mapped()
    {
        var report = await GetReportAsync(options =>
        {
            options.MutualTls.Enabled = true;
            options.DPoP.SupportedDPoPSigningAlgorithms = [];
        });

        report.ShouldContain("No DPoP proof algorithms are configured, so DPoP is effectively disabled. mTLS is enabled to provide sender-constrained tokens.");
    }

    [Fact]
    public async Task empty_client_assertion_signing_algorithms_are_mapped()
    {
        var report = await GetReportAsync(options => options.SupportedClientAssertionSigningAlgorithms = []);

        report.ShouldContain("No algorithms are configured for private_key_jwt client assertions, so any algorithm is accepted.");
    }

    [Fact]
    public async Task empty_request_object_signing_algorithms_are_mapped()
    {
        var report = await GetReportAsync(options => options.SupportedRequestObjectSigningAlgorithms = []);

        report.ShouldContain("No algorithms are configured for JAR request objects, so any algorithm is accepted.");
    }

    [Fact]
    public async Task client_assertion_signing_algorithms_are_mapped()
    {
        var report = await GetReportAsync(options => options.SupportedClientAssertionSigningAlgorithms = ["RS256"]);

        report.ShouldContain("No FAPI 2.0 compliant algorithms are configured for private_key_jwt client assertions. Found: RS256.");
    }

    [Fact]
    public async Task request_object_signing_algorithms_are_mapped()
    {
        var report = await GetReportAsync(options => options.SupportedRequestObjectSigningAlgorithms = ["RS384"]);

        report.ShouldContain("No FAPI 2.0 compliant algorithms are configured for JAR request objects. Found: RS384.");
    }

    [Fact]
    public async Task dpop_signing_algorithms_are_mapped()
    {
        var report = await GetReportAsync(options => options.DPoP.SupportedDPoPSigningAlgorithms = ["RS512"]);

        report.ShouldContain("No FAPI 2.0 compliant algorithms are configured for DPoP proofs. Found: RS512.");
    }

    [Fact]
    public async Task disabled_discovery_endpoint_is_mapped()
    {
        var report = await GetReportAsync(options => options.Endpoints.EnableDiscoveryEndpoint = false);

        report.ShouldContain("Discovery endpoint is not enabled.");
    }

    [Fact]
    public async Task issuer_uri_is_mapped()
    {
        var report = await GetReportAsync(options => options.IssuerUri = "http://issuer.example.com");

        report.ShouldContain("IssuerUri 'http://issuer.example.com' is not an absolute https URL.");
    }

    [Fact]
    public async Task allow_unregistered_pushed_redirect_uris_is_mapped()
    {
        var client = new Client
        {
            ClientId = "par-only",
            AllowedGrantTypes = GrantTypes.Code,
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedScopes = { "openid" }
        };

        var report = await GetReportAsync(
            options => options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true,
            clients: [client]);

        report.ShouldContain("Redirect URIs are supplied via an authenticated PAR request");
    }

    private static async Task<string> GetReportAsync(
        Action<IdentityServerOptions>? configureOptions = null,
        IEnumerable<Client>? clients = null,
        Action<IIdentityServerBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.AddAuthorization();

        var identityServer = builder.Services
            .AddIdentityServer(options =>
            {
                // Keep tests independent of key storage on disk; tests that need keys add them.
options.KeyManagement.Enabled = false;
                options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.Zero;
                configureOptions?.Invoke(options);
            })
            .AddInMemoryClients(clients ?? [])
            .AddInMemoryIdentityResources([new IdentityResources.OpenId()])
            .AddConformanceReport(options =>
            {
                options.Enabled = true;
                options.ConfigureAuthorization = policy => policy.RequireAssertion(_ => true);
            });
        configureBuilder?.Invoke(identityServer);

        await using var app = builder.Build();
        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapConformanceReport();
        await app.StartAsync();

        var response = await app.GetTestServer().CreateClient().GetAsync("/_duende/conformance-report");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Messages contain quotes, which the report HTML-encodes.
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private sealed class InMemorySigningKeyStore : ISigningKeyStore
    {
        private readonly List<SerializedKey> _keys = [];

        public Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct) =>
            Task.FromResult<IReadOnlyCollection<SerializedKey>>(_keys.ToList());

        public Task StoreKeyAsync(SerializedKey key, Ct ct)
        {
            _keys.Add(key);
            return Task.CompletedTask;
        }

        public Task DeleteKeyAsync(string id, Ct ct)
        {
            _ = _keys.RemoveAll(k => k.Id == id);
            return Task.CompletedTask;
        }
    }
}
