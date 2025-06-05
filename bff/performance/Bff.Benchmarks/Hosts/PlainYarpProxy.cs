// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Bff.Benchmarks.Hosts;

public class PlainYarpProxy : Host
{
    public PlainYarpProxy(Uri api)
    {
        OnConfigureServices += services =>
        {
            services.AddReverseProxy()
                .LoadFromMemory(
                    [
                        new RouteConfig()
                        {
                            RouteId = "1",
                            ClusterId = "cluster_id",
                            Match = new RouteMatch()
                            {
                                Path = "/yarp/{**catch-all}",
                            }
                        }
                    ],
                    [
                        new ClusterConfig()
                        {
                            ClusterId = "cluster_id",

                            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                            {
                                { "destination1", new DestinationConfig { Address = api.ToString() } }
                            }
                        }
                    ]);
        };
        OnConfigure += app =>
        {
            app.UseRouting();
            app.MapReverseProxy();
        };
    }
}
