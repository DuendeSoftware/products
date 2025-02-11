using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Headers
{
    public class ApiAndBffUseForwardedHeaders : BffIntegrationTestBase
    {
        public ApiAndBffUseForwardedHeaders(ITestOutputHelper output) : base(output)
        {
            BffHost.UseForwardedHeaders = true;
            ApiHost.UseForwardedHeaders = true;
        }

        [Fact]
        public async Task bff_host_name_should_propagate_to_api()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.Should().Be2XXSuccessful();
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

            var host = apiResult.RequestHeaders["Host"].Single();
            host.ShouldBe("app");
        }
        
        [Fact]
        public async Task forwarded_host_name_without_header_forwarding_propagate_to_api()
        {
            await BffHost.InitializeAsync();
            
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
            req.Headers.Add("x-csrf", "1");
            req.Headers.Add("X-Forwarded-Host", "external");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.Should().Be2XXSuccessful();
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

            var host = apiResult.RequestHeaders["Host"].Single();
            host.ShouldBe("external");
        }
        
        [Fact]
        public async Task forwarded_host_name_with_header_forwarding_should_propagate_to_api()
        {
            await BffHost.InitializeAsync();
            
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
            req.Headers.Add("x-csrf", "1");
            req.Headers.Add("X-Forwarded-Host", "external");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.Should().Be2XXSuccessful();
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

            var host = apiResult.RequestHeaders["Host"].Single();
            host.ShouldBe("external");
        }
    }
}