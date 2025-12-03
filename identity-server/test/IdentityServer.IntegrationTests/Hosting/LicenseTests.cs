// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public class LicenseTests : IDisposable
{
    private string client_id = "client";
    private string client_secret = "secret";
    private string scope_name = "api";

    private IdentityServerPipeline _mockPipeline = new();

    public LicenseTests()
    {
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = client_id,
            ClientSecrets = [new Secret(client_secret.Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["api"],
        });
        _mockPipeline.ApiScopes = [new ApiScope(scope_name)];
    }

    public void Dispose()
    {
        // Some of our tests involve copying test license files so that the pipeline will read them.
        // This should ensure that they are cleaned up after each test.
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var path1 = Path.Combine(contentRoot, "Duende_License.key");
        if (File.Exists(path1))
        {
            File.Delete(path1);
        }
        var path2 = Path.Combine(contentRoot, "Duende_IdentityServer_License.key");
        if (File.Exists(path2))
        {
            File.Delete(path2);
        }
    }
    [Theory]
    [InlineData("RedistEnterprise", TestLicenses.RedistributionEnterpriseLicense, false)]
    [InlineData("RedistBusiness", TestLicenses.RedistributionBusinessLicense, true)]
    [InlineData("RedistStarter", TestLicenses.RedistributionStarterLicense, true)]
    public async Task excess_issuers_log_errors_for_redistribution_licenses(string label, string key, bool shouldLog)
    {
        _mockPipeline = new(); // Reset pipeline to ensure we don't reuse previous pipeline with different/no key
        _mockPipeline.LicenseKey = key;
        _mockPipeline.Clock.SetUtcNow(new DateTimeOffset(2024, 11, 1, 0, 0, 0, TimeSpan.Zero));
        _mockPipeline.Initialize(enableLogging: true);

        // First request with default host (https://server)
        await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.DiscoveryEndpoint);

        // Second request with a different hostname to trigger a different issuer
        // Create a new HttpClient with a custom handler that modifies the Host header
        var client2 = CreateClient("server2");
        await client2.GetAsync(IdentityServerPipeline.DiscoveryEndpoint);

        var issuersMessage =
            "Your license for IdentityServer includes 1 issuers but you have processed requests for 2 issuers. This indicates that requests for each issuer are being sent to this instance of IdentityServer, which may be due to a network infrastructure configuration issue. If you intend to use multiple issuers, please contact contact@duendesoftware.com at _test or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The issuers used were https://server, https://server2.";
        if (shouldLog)
        {
            _mockPipeline.MockLogger.LogMessages.ShouldContain(issuersMessage);
        }
        else
        {
            _mockPipeline.MockLogger.LogMessages.ShouldNotContain(issuersMessage);
        }
    }

    private HttpClient CreateClient(string issuer)
    {
        var customHandler = new HostOverrideHandler(_mockPipeline.Handler, issuer);
        var client2 = new HttpClient(customHandler) { BaseAddress = new Uri($"https://{issuer}") };
        return client2;
    }

    // Helper class to override the Host header in requests
    private class HostOverrideHandler : DelegatingHandler
    {
        private readonly string _host;

        public HostOverrideHandler(HttpMessageHandler innerHandler, string host) : base(innerHandler) => _host = host;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Modify the request to use a different host
            var builder = new UriBuilder(request.RequestUri!)
            {
                Host = _host
            };
            request.RequestUri = builder.Uri;
            request.Headers.Host = _host;

            return base.SendAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task unlicensed_protocol_requests_log_a_warning()
    {
        var threshold = 5u;
        _mockPipeline.OnPostConfigure += builder =>
        {
            var counter = builder.ApplicationServices.GetRequiredService<ProtocolRequestCounter>();
            counter.Threshold = threshold;
        };
        _mockPipeline.Initialize(enableLogging: true);

        // The actual protocol parameters aren't the point of this test, this could be any protocol request
        var data = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", client_id },
            { "client_secret", client_secret },
            { "scope", scope_name },
        };
        var form = new FormUrlEncodedContent(data);

        for (var i = 0; i < threshold + 1; i++)
        {
            await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);
        }

        _mockPipeline.MockLogger.LogMessages.ShouldContain(
            $"You are using IdentityServer in trial mode and have exceeded the trial threshold of {threshold} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. See https://duende.link/l/trial for more information.");
    }


    private static class TestLicenses
    {
        // Redistribution licenses
        public const string RedistributionEnterpriseLicense =
            "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiRW50ZXJwcmlzZSIsImlkIjoiNjY4NCIsImZlYXR1cmUiOiJpc3YiLCJwcm9kdWN0IjoiVEJEIn0.Y-bbdSsdHHzrJs40CpEIsgi7ugc8ScTa2ArCuL-wM__O6znygAUTGOLrzhFaeRibud5lNXSYaA0vkkF1UFQS4HJF_wTMe5pYH4DT1vVYaVXd9Xyqn-klQvBLcoo4JAoFNau0Az-czbo6UBkejKn-7QDnJunFcHaYenDpzgsXHiaK4mkIMRI_OnBYKegNa_xvYRRzorKkT3x8q1n7vUnx80-b6Jf2Y0u6fPsLwE2Or-VBXRpTGL20MBtcPS56wQDDdl4eKkW716lHS-Iyh5KW3K5HVKRxd86ot18MY6Bd3PPUQocFYXd5KhTH_YKvwVqAUkc0MhHYJLFV_5Q8qSRECA";
        public const string RedistributionBusinessLicense =
            "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiQnVzaW5lc3MiLCJpZCI6IjY2ODMiLCJmZWF0dXJlIjoiaXN2IiwicHJvZHVjdCI6IlRCRCJ9.rYDrY6UUKgZfnfx7GA1PILYj9XICIjC9aS06P8rUAuXYjxiagEIEkacKt3GcccJI6k0lMb6qbd3Hv-Q9rDDyDSxUZxwvGzVlhRrIditOI38FoN3trUd5RU6S7A_RSDd4uV0L1T8NKUKGlOvu8_7egcIy-E8q34GA5BNU2lV2Gsaa7yWAyTKZh7YPIP4y_TwLxOcw2GRn6dQq73-O_XaAIf0AxFowW1GsiBrirzE_TKwJ8VkbvN3O-yVT-ntPvoK0tHRKoG5yh8GPuDORQtlis_5bZHHFzazXVMul1rkYWSU9OhIdixvI44q1q1_5VGoGJ3SLFIFsdWM0ZvnPx7_Bqg";
        public const string RedistributionStarterLicense =
            "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiU3RhcnRlciIsImlkIjoiNjY4MiIsImZlYXR1cmUiOiJpc3YiLCJwcm9kdWN0IjoiVEJEIn0.Ag4HLR1TVJ2VYgW1MJbpIHvAerx7zaHoM4CLu7baipsZVwc82ZkmLUeO_yB3CqN7N6XepofwZ-RcloxN8UGZ6qPRGQPE1cOMrp8YqxLOI38gJbxALOBG5BB6YTCMf_TKciXn1c3XhrsxVDayMGxAU68fKDCg1rnamBehZfXr2uENipNPkGDh_iuRw2MUgeGY96CGvwCC5R0E6UnvGZbjQ7dFYV-CkAHuE8dEAr0pX_gD77YsYcSxq5rNUavcNnWV7-3knFwozNqi02wTDpcKtqaL2mAr0nRof1E8Df9C8RwCTWXSaWhr9_47W2I1r_IhLYS2Jnq6m_3BgAIvWL4cjQ";
    }
}
