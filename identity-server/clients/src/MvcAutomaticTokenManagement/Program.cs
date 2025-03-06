// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Serilog;
using Serilog.Events;

namespace MvcAutomaticTokenManagement;

public class Program
{
    public static int Main(string[] args)
    {
        Console.Title = "MvcAutomaticTokenManagement";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Duende.IdentityModel", LogEventLevel.Debug)
            .MinimumLevel.Override("Duende.AccessTokenManagement", LogEventLevel.Debug)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
            .MinimumLevel.Override("MvcAutomaticTokenManagement", LogEventLevel.Debug)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
            .CreateLogger();

        try
        {
            Log.Information("Starting host...");
            CreateHostBuilder(args).Build().Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly.");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .UseSerilog();
}
