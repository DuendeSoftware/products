// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityServerHost;
using Microsoft.AspNetCore;

namespace SqlServer;

class Program
{
    public static void Main(string[] args)
    {
        var host = BuildWebHost(args);
        SeedData.EnsureSeedData(host.Services);
    }

    public static IWebHost BuildWebHost(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build();
}