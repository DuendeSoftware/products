// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.ConformanceReport.Internal.Models;
using Duende.ConformanceReport.Licensing;
using Duende.ConformanceReport.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Endpoints;

public class ConformanceReportEndpointTests
{
    private static ConformanceReportServerOptions CreateDefaultServerOptions() =>
        new()
        {
            PushedAuthorizationEndpointEnabled = true,
            PushedAuthorizationRequired = true,
            PushedAuthorizationLifetime = 600,
            AllowUnregisteredPushedRedirectUris = false,
            LoopbackRedirectUrisEnabled = false,
            DiscoveryEndpointEnabled = true,
            MutualTlsEnabled = true,
            SupportedClientAssertionSigningAlgorithms = ["PS256", "ES256"],
            SupportedRequestObjectSigningAlgorithms = ["PS256", "ES256"],
            DPoPSigningAlgorithms = ["PS256", "ES256"],
            TokenSigningAlgorithms = ["PS256", "ES256"],
            JwtValidationClockSkew = TimeSpan.FromMinutes(5),
            EmitIssuerIdentificationResponseParameter = true
        };

    private static ConformanceReportClient CreateTestClient(string clientId = "test-client") =>
        new()
        {
            ClientId = clientId,
            ClientName = "Test Client",
            AllowedGrantTypes = [ConformanceReportGrantTypes.AuthorizationCode],
            RequirePkce = true,
            AllowPlainTextPkce = false,
            RedirectUris = ["https://example.com/callback"],
            RequireClientSecret = true,
            ClientSecretTypes = [ConformanceReportSecretTypes.JsonWebKey],
            RequirePushedAuthorization = true,
            RequireDPoP = true,
            DPoPValidationMode = ConformanceReportDPoPValidationMode.Nonce,
            AuthorizationCodeLifetime = 60,
            AllowOfflineAccess = true,
            RefreshTokenUsage = ConformanceReportTokenUsage.OneTimeOnly,
            AllowAccessTokensViaBrowser = false,
            RequireRequestObject = false
        };

    private static ConformanceReportOptions CreateDefaultOptions(bool enabled = true) =>
        new()
        {
            Enabled = enabled,
            EnableOAuth21Assessment = true,
            EnableFapi2SecurityAssessment = true,
            PathPrefix = "__duende",
            AuthorizationPolicyName = "conformance.report",
            ConfigureAuthorization = null // Skip policy registration in tests
        };

    private static ConformanceReportEndpoint CreateEndpoint(
        IConformanceReportClientStore? clientStore = null,
        ConformanceReportOptions? options = null,
        ConformanceReportLicenseInfo? licenseInfo = null)
    {
        options ??= CreateDefaultOptions();
        clientStore ??= new InMemoryClientStore([CreateTestClient()]);

        var serverOptions = CreateDefaultServerOptions();
        var httpContextAccessor = new TestHttpContextAccessor();

        var assessmentService = new ConformanceReportAssessmentService(
            Options.Create(options),
            () => serverOptions,
            clientStore,
            httpContextAccessor,
            licenseInfo);

        var endpoint = new ConformanceReportEndpoint(
            assessmentService,
            ConformanceReportLicenseValidator.CreateForTests(),
            Options.Create(options),
            NullLogger<ConformanceReportEndpoint>.Instance);

        return endpoint;
    }

    private sealed class InMemoryClientStore(IEnumerable<ConformanceReportClient> clients) : IConformanceReportClientStore
    {
        public Task<IEnumerable<ConformanceReportClient>> GetAllClientsAsync(Ct ct)
            => Task.FromResult(clients);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = CreateHttpContext();

        private static DefaultHttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Scheme = "https",
                    Host = new HostString("localhost"),
                    Path = "/__duende/conformance/v1"
                }
            };
            return context;
        }
    }

    public class HtmlEndpointTests
    {
        private readonly Ct _ct = TestContext.Current.CancellationToken;

        [Fact]
        public async Task get_html_report_when_enabled_returns_html_content()
        {
            var endpoint = CreateEndpoint();

            var result = await endpoint.GetHtmlReportAsync(_ct);

            _ = result.ShouldNotBeNull();
            _ = result.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult>();
            var contentResult = (Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult)result;
            contentResult.ContentType.ShouldBe("text/html");
        }

        [Fact]
        public async Task get_html_report_when_disabled_returns_not_found()
        {
            var options = CreateDefaultOptions(enabled: false);
            var endpoint = CreateEndpoint(options: options);

            var result = await endpoint.GetHtmlReportAsync(_ct);

            _ = result.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
        }

        [Fact]
        public async Task get_html_report_with_license_does_not_bleed_into_url()
        {
            var licenseInfo = new ConformanceReportLicenseInfo
            {
                CompanyName = "Test Company",
                Edition = "Enterprise",
                SerialNumber = 1234,
                Expiration = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
            };
            var endpoint = CreateEndpoint(licenseInfo: licenseInfo);

            var result = await endpoint.GetHtmlReportAsync(_ct);

            var contentResult = (ContentHttpResult)result;
            var html = contentResult.ResponseContent!;

            // The license info should be present
            html.ShouldContain("Test Company | Enterprise | #1234 | Expires 2025-12-31");
            // The URL should NOT contain the expiration date (the bug!)
            html.ShouldNotContain("conformance-report2025-12-31");
        }
    }
}
