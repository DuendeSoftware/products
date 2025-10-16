// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore;

namespace IdentityServerDb;

// TODO: Upgrade this client to remove dependancy on IWebHost & WebHostBuilder
#pragma warning disable ASPDEPR008 // IWebHost is deprecated in net10.0

internal class Program
{
    public static void Main(string[] args)
    {
        var host = BuildWebHost(args);
        SeedData.EnsureSeedData(host.Services);

        // Exit the application
        Console.WriteLine("Exiting application...");
        Environment.Exit(0);
    }

    public static IWebHost BuildWebHost(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build();
}
