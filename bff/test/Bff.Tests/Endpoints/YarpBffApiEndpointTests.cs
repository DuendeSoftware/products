// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;

namespace Duende.Bff.Tests.Endpoints;

public class YarpBffApiEndpointTests : BffTestBase
{
    [Fact]
    public async Task yarp_route_marked_as_bff_api_endpoint_does_not_call_the_api_when_bff_middleware_is_missing()
    {
        Bff.UseBffPlacement = UseBffPlacement.Omitted;
        Bff.OnConfigureBff += bff => bff.AddYarpConfig([Some.RouteConfig()], [Some.ClusterConfig(Api)]);
        Bff.OnConfigureApp += app => app.MapReverseProxy().AsBffApiEndpoint();
        await ConfigureBff(BffSetupType.V4Bff);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Bff.BrowserClient.GetAsync(Bff.Url(The.PathAndSubPath)));

        ex.Message.ShouldContain("app.UseBff");
        Api.ReceivedRequestCount.ShouldBe(0);
    }
}
