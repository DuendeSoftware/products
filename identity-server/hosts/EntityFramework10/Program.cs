// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using Duende.IdentityServer.Licensing;
using IdentityServerHost;
using Serilog;

SerilogDefaults.Bootstrap();

try
{
    var builder = WebApplication.CreateBuilder(args);

    Console.Title = builder.Environment.ApplicationName;
    Log.Information("{EnvironmentApplicationName} Starting up", builder.Environment.ApplicationName);

    builder.ConfigureSerilogDefaults();
    builder.AddServiceDefaults()
        .AddOpenTelemetryMeters(Duende.IdentityServer.UI.Pages.Telemetry.ServiceName);

    var app = builder
        .ConfigureServices()
        .ConfigurePipeline();

    if (app.Environment.IsDevelopment())
    {
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            var usage = app.Services.GetRequiredService<LicenseUsageSummary>();
            Console.Write(Summary(usage));
        });
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

static string Summary(LicenseUsageSummary usage)
{
    var sb = new StringBuilder();
    sb.AppendLine("IdentityServer Usage Summary:");
    sb.AppendLine(CultureInfo.InvariantCulture, $"  License: {usage.LicenseEdition}");
    var features = usage.FeaturesUsed.Count > 0 ? string.Join(", ", usage.FeaturesUsed) : "None";
    sb.AppendLine(CultureInfo.InvariantCulture, $"  Business and Enterprise Edition Features Used: {features}");
    sb.AppendLine(CultureInfo.InvariantCulture, $"  {usage.ClientsUsed.Count} Client Id(s) Used");
    sb.AppendLine(CultureInfo.InvariantCulture, $"  {usage.IssuersUsed.Count} Issuer(s) Used");

    return sb.ToString();
}
