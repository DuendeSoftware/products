// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.HttpOverrides;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Headers;

public class ApiAndBffUseForwardedHeaders : BffTestBase, IAsyncLifetime
{
    public ApiAndBffUseForwardedHeaders(ITestOutputHelper output) : base(output)
    {
        Bff.OnConfigure += app =>
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost
            });
        };

        Bff.OnConfigureBff += bff => bff.AddRemoteApis();

        Api.OnConfigure += app =>
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost
            });
        };

        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapRemoteBffApiEndpoint(The.Path, Api.Url().ToString());
        };

    }

    [Fact]
    public async Task bff_host_name_should_propagate_to_api()
    {

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(The.SubPath);

        var host = apiResult.RequestHeaders["Host"].Single();
        host.ShouldBe(Bff.Url().Host);
    }

    [Fact]
    public async Task forwarded_host_name_with_header_forwarding_should_propagate_to_api()
    {
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(The.SubPath,
            headers: new()
            {
                ["X-Forwarded-Host"] = "external"
            });

        var host = apiResult.RequestHeaders["Host"].Single();
        host.ShouldBe("external");
    }
}
