// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Hosts.Bff.Performance.Services;

public class ApiHostedService(IOptions<ApiSettings> apiSettings) : BackgroundService
{
    public ApiSettings Settings { get; } = apiSettings.Value;

    protected override Task ExecuteAsync(Ct stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.AddServiceDefaults();

        // Configure Kestrel to listen on the specified Uri
        _ = builder.WebHost.UseUrls(Settings.ApiUrl.ToString());
        var app = builder.Build();


        _ = app.UseRouting();

        _ = app.MapGet("/", () => "ok");
        return app.RunAsync(stoppingToken);

    }
}
