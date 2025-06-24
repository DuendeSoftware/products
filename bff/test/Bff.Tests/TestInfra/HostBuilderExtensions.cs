// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;

namespace Duende.Bff.Tests.TestInfra;

public static class HostBuilderExtensions
{
    internal static IBffApplicationBuilder UsingTestServer(this IBffApplicationBuilder appBuilder)
    {
        appBuilder.UsingServiceDefaults((t, b) =>
        {
            if (b is WebApplicationBuilder webAppBuilder)
            {
                webAppBuilder.WebHost.UseTestServer();
            }
        });

        return appBuilder;
    }
    internal static Uri GetBffUri(this IHost host, BffApplicationPartType forPart = BffApplicationPartType.BffEndpoint)
    {
        if (forPart != BffApplicationPartType.BffEndpoint)
        {
            throw new NotSupportedException("Only BffApplication part type is supported for URI retrieval.");
        }

        var service = host.Services.GetService<BffEndpointHostedService>() ?? throw new InvalidOperationException("BFF application not registered. ");

        if (!(service.Services.GetService<IServer>() is TestServer))
        {
            return new Uri("https://localhost:" + new Uri(service.App.Urls.First()).Port);
        }

        return new Uri("https://bff-server/");
    }

    internal static HttpMessageHandler GetTestHandler(this IHost host,
        BffApplicationPartType forPart = BffApplicationPartType.BffEndpoint)
    {
        if (forPart != BffApplicationPartType.BffEndpoint)
        {
            throw new NotSupportedException("Only BffApplication part type is supported for URI retrieval.");
        }

        var service = host.Services.GetService<BffEndpointHostedService>() ?? throw new InvalidOperationException("BFF application not registered. ");

        if (!(service.Services.GetService<IServer>() is TestServer testServer))
        {
            throw new InvalidOperationException("BFF application is not running in a test server. ");
        }

        return testServer.CreateHandler();

    }
}

