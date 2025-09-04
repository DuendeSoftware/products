// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Discovery;

public abstract class DiscoveryEndpointTestsBase
{
    protected static IdentityServerPipeline CreatePipelineWithJwtBearer()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += svcs =>
            svcs.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
        pipeline.Initialize();
        return pipeline;
    }

    public static IEnumerable<object[]> NullOrEmptySupportedAlgorithms() =>
        new List<object[]>
        {
            new object[] { Enumerable.Empty<string>() },
            new object[] { null }
        };
}
