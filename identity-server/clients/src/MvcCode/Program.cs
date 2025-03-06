// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Serilog;
using Serilog.Events;

namespace MvcCode;

public class Program
{
    public static void Main(string[] args)
    {
        Console.Title = "MvcCode";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Duende.IdentityModel", LogEventLevel.Debug)
            .MinimumLevel.Override("MvcCode", LogEventLevel.Debug)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
            .CreateLogger();

        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .UseSerilog();
}
