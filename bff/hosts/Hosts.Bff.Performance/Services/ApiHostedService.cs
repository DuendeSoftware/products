using Microsoft.Extensions.Options;

namespace Hosts.Bff.Performance.Services;

public class ApiHostedService(IOptions<ApiSettings> apiSettings) : BackgroundService
{
    public ApiSettings Settings { get; } = apiSettings.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddServiceDefaults();

        // Configure Kestrel to listen on the specified Uri
        builder.WebHost.UseUrls(Settings.ApiUrl.ToString());
        var app = builder.Build();


        app.UseRouting();

        app.MapGet("/", () => "ok");
        return app.RunAsync(stoppingToken);

    }
}