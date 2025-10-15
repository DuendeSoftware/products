// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;

namespace Duende.Bff.Tests.Headers;

public class ApiUseForwardedHeaders : BffIntegrationTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ApiUseForwardedHeaders(ITestOutputHelper output)
        : base(output) => ApiHost.UseForwardedHeaders = true;

    [Fact]
    public async Task bff_host_name_should_propagate_to_api()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
        req.Headers.Add("x-csrf", "1");
        var response = await BffHost.BrowserClient.SendAsync(req, _ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync(_ct);
        var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

        var host = apiResult.RequestHeaders["Host"].Single();
        host.ShouldBe("app");
    }

    [Fact]
    public async Task forwarded_host_name_should_not_propagate_to_api()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
        req.Headers.Add("x-csrf", "1");
        req.Headers.Add("X-Forwarded-Host", "external");
        var response = await BffHost.BrowserClient.SendAsync(req, _ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync(_ct);
        var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

        var host = apiResult.RequestHeaders["Host"].Single();
        host.ShouldBe("app");
    }
}
