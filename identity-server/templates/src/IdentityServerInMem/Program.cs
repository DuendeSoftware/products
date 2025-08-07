using System.Globalization;
using System.Text;
using Duende.IdentityServer.Licensing;
using IdentityServerInMem;
using Serilog;
using Serilog.Filters;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

Log.Information("Starting up");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Set up logging to write regular entries to console, and diagnostics data to a file.
    // See https://docs.duendesoftware.com/identityserver/diagnostics/data
    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Logger(consoleLogger =>
        {
            consoleLogger
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", formatProvider: CultureInfo.InvariantCulture)
                .Filter.ByExcluding(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
        })
        .WriteTo.Logger(fileLogger =>
        {
            fileLogger
                .WriteTo.File("./diagnostics/diagnostic.log",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 1024 * 1024 * 10, // 10 MB
                    rollOnFileSizeLimit: true,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                    formatProvider: CultureInfo.InvariantCulture)
                .Filter.ByIncludingOnly(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
        })
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

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
