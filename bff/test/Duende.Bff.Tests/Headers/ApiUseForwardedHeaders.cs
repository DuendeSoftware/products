using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Xunit;

namespace Duende.Bff.Tests.Headers
{
    public class ApiUseForwardedHeaders : BffIntegrationTestBase
    {
        public ApiUseForwardedHeaders()
        {
            ApiHost = new ApiHost(IdentityServerHost, "scope1", useForwardedHeaders: true);
            ApiHost.InitializeAsync().Wait();

            BffHost = new BffHost(IdentityServerHost, ApiHost, "spa", useForwardedHeaders: false);
            BffHost.InitializeAsync().Wait();
        }
        
        [Fact]
        public async Task bff_host_name_should_propagate_to_api()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);

            var host = apiResult.RequestHeaders["Host"].Single();
            host.Should().Be("app");
        }
        
        [Fact]
        public async Task forwarded_host_name_should_not_propagate_to_api()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
            req.Headers.Add("x-csrf", "1");
            req.Headers.Add("X-Forwarded-Host", "external");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);

            var host = apiResult.RequestHeaders["Host"].Single();
            host.Should().Be("app");
        }
    }
}