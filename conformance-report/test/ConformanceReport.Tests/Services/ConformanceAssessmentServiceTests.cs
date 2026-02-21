// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Services;

public class ConformanceAssessmentServiceTests
{
    private static ConformanceReportServerOptions CreateDefaultServerOptions(
        bool parEnabled = true,
        bool parRequired = true,
        bool mtlsEnabled = true,
        IReadOnlyCollection<string>? signingAlgorithms = null,
        bool emitIssuer = true,
        bool useHttp303Redirects = true) =>
        new()
        {
            PushedAuthorizationEndpointEnabled = parEnabled,
            PushedAuthorizationRequired = parRequired,
            PushedAuthorizationLifetime = 600,
            MutualTlsEnabled = mtlsEnabled,
            SupportedSigningAlgorithms = signingAlgorithms ?? ["PS256", "ES256"],
            JwtValidationClockSkew = TimeSpan.FromMinutes(5),
            EmitIssuerIdentificationResponseParameter = emitIssuer,
            UseHttp303Redirects = useHttp303Redirects
        };

    private static ConformanceReportClient CreateCompliantClient(string clientId = "compliant-client") =>
        new()
        {
            ClientId = clientId,
            ClientName = "Compliant Client",
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

    private static ConformanceReportClient CreateNonCompliantClient(string clientId = "non-compliant-client") =>
        new()
        {
            ClientId = clientId,
            ClientName = "Non-Compliant Client",
            AllowedGrantTypes = [ConformanceReportGrantTypes.Implicit], // Not allowed
            RequirePkce = false, // Required
            AllowPlainTextPkce = true, // Should be false
            RedirectUris = ["https://*.example.com/callback"], // Wildcard
            RequireClientSecret = false, // For FAPI should be true
            ClientSecretTypes = [ConformanceReportSecretTypes.SharedSecret],
            RequirePushedAuthorization = false,
            RequireDPoP = false,
            DPoPValidationMode = ConformanceReportDPoPValidationMode.None,
            AuthorizationCodeLifetime = 300, // Too long
            AllowOfflineAccess = false,
            RefreshTokenUsage = ConformanceReportTokenUsage.ReUse,
            AllowAccessTokensViaBrowser = true, // Not allowed
            RequireRequestObject = false
        };

    private static ConformanceReportOptions CreateDefaultOptions(
        bool enableOAuth21 = true,
        bool enableFapi2 = true) =>
        new()
        {
            Enabled = true,
            EnableOAuth21Assessment = enableOAuth21,
            EnableFapi2SecurityAssessment = enableFapi2,
            PathPrefix = "__duende",
            AuthorizationPolicyName = "conformance.report",
            ConfigureAuthorization = null // Skip policy registration in tests
        };

    private static ConformanceReportAssessmentService CreateService(
        ConformanceReportOptions? options = null,
        ConformanceReportServerOptions? serverOptions = null,
        IEnumerable<ConformanceReportClient>? clients = null)
    {
        options ??= CreateDefaultOptions();
        serverOptions ??= CreateDefaultServerOptions();
        clients ??= [CreateCompliantClient()];

        var clientStore = new InMemoryClientStore(clients);
        var httpContextAccessor = new TestHttpContextAccessor();

        return new ConformanceReportAssessmentService(
            Options.Create(options),
            () => serverOptions,
            clientStore,
            httpContextAccessor);
    }

    private sealed class InMemoryClientStore(IEnumerable<ConformanceReportClient> clients) : IConformanceReportClientStore
    {
        public Task<IEnumerable<ConformanceReportClient>> GetAllClientsAsync(CancellationToken ct) => Task.FromResult(clients);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = CreateHttpContext();

        private static DefaultHttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/__duende/conformance/v1";
            return context;
        }
    }

    public class ReportGenerationTests
    {
        private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
        [Fact]
        public async Task GenerateReportWithBothProfilesEnabledReturnsCompleteReport()
        {
            var service = CreateService();

            var report = await service.GenerateReportAsync(_ct);

            _ = report.ShouldNotBeNull();
            _ = report.Profiles.ShouldNotBeNull();
            _ = report.Profiles.OAuth21.ShouldNotBeNull();
            _ = report.Profiles.Fapi2Security.ShouldNotBeNull();
        }

        [Fact]
        public async Task GenerateReportWithOnlyOAuth21EnabledReturnsOAuth21Only()
        {
            var options = CreateDefaultOptions(enableOAuth21: true, enableFapi2: false);
            var service = CreateService(options: options);

            var report = await service.GenerateReportAsync(_ct);

            _ = report.Profiles.OAuth21.ShouldNotBeNull();
            report.Profiles.Fapi2Security.ShouldBeNull();
        }

        [Fact]
        public async Task GenerateReportWithOnlyFapi2EnabledReturnsFapi2Only()
        {
            var options = CreateDefaultOptions(enableOAuth21: false, enableFapi2: true);
            var service = CreateService(options: options);

            var report = await service.GenerateReportAsync(_ct);

            report.Profiles.OAuth21.ShouldBeNull();
            _ = report.Profiles.Fapi2Security.ShouldNotBeNull();
        }

        [Fact]
        public async Task GenerateReportSetsAssessedAtTimestamp()
        {
            var service = CreateService();
            var beforeTime = DateTimeOffset.UtcNow;

            var report = await service.GenerateReportAsync(_ct);

            var afterTime = DateTimeOffset.UtcNow;
            report.AssessedAt.ShouldBeGreaterThanOrEqualTo(beforeTime);
            report.AssessedAt.ShouldBeLessThanOrEqualTo(afterTime);
        }

        [Fact]
        public async Task GenerateReportWithMixedClientsCalculatesCorrectSummary()
        {
            var clients = new[]
            {
                CreateCompliantClient("pass1"),
                CreateCompliantClient("pass2"),
                CreateNonCompliantClient("fail1")
            };
            var service = CreateService(clients: clients);

            var report = await service.GenerateReportAsync(_ct);

            // Overall summary
            report.OverallSummary.TotalClients.ShouldBe(3);
            report.Status.ShouldBe(ConformanceReportStatus.Fail);

            // OAuth 2.1 summary
            var oauth21Summary = report.Profiles.OAuth21!.Summary;
            oauth21Summary.TotalClients.ShouldBe(3);
            oauth21Summary.PassingClients.ShouldBe(2);
            oauth21Summary.FailingClients.ShouldBe(1);
        }
    }
}
