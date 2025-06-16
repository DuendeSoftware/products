// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Xunit.Abstractions;
using ApiHost = Duende.Bff.Tests.TestInfra.ApiHost;

namespace Duende.Bff.Tests.Headers;

public class General(ITestOutputHelper output) : BffTestBase(output)
{
    [Theory, MemberData(nameof(AllSetups))]
    public async Task local_endpoint_should_receive_standard_headers(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c))
                .RequireAuthorization();
        };
        ConfigureBff(setup);
        await InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiCallDetails>(json).ShouldNotBeNull();

        apiResult.RequestHeaders.Count.ShouldBe(3);
        apiResult.RequestHeaders["Host"].Single().ShouldBe(Bff.Url().Host);
        apiResult.RequestHeaders["x-csrf"].Single().ShouldBe("1");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task custom_header_should_be_forwarded(BffSetupType setup)
    {
        Bff.OnConfigureBff += bff => bff.AddRemoteApis();
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.None);

        };
        ConfigureBff(setup);
        await InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        req.Headers.Add("x-custom", "custom");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiCallDetails>(json).ShouldNotBeNull();

        apiResult.RequestHeaders["Host"].Single().ShouldBe("api");
        apiResult.RequestHeaders["x-custom"].Single().ShouldBe("custom");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task custom_header_should_be_forwarded_and_xforwarded_headers_should_be_created(BffSetupType setup)
    {
        Bff.OnConfigureBff += bff => bff.AddRemoteApis();
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.None);

        };
        ConfigureBff(setup);
        await InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        req.Headers.Add("x-custom", "custom");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiCallDetails>(json).ShouldNotBeNull();

        apiResult.RequestHeaders["X-Forwarded-Host"].Single().ShouldBe(Bff.Url().Host);
        apiResult.RequestHeaders["X-Forwarded-Proto"].Single().ShouldBe("https");
        apiResult.RequestHeaders["Host"].Single().ShouldBe("api");
        apiResult.RequestHeaders["x-custom"].Single().ShouldBe("custom");
    }
}
