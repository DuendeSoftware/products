// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using MvcDPoP;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Duende.IdentityModel", LogEventLevel.Debug)
    .MinimumLevel.Override("Duende.AccessTokenManagement", LogEventLevel.Debug)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
    .MinimumLevel.Override("MvcDPoP", LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
    .CreateLogger();

try
{
    var builder = WebApplication
        .CreateBuilder(args);

    builder
        .AddServiceDefaults();

    builder
        .ConfigureServices()
        .ConfigurePipeline()
        .Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, messageTemplate: "Unhandled exception");
}
finally
{
    Log.Information(messageTemplate: "Shut down complete");
    Log.CloseAndFlush();
}
