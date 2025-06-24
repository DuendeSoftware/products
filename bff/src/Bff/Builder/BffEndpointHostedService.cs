// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Builder;

internal sealed class BffEndpointHostedService(
    [FromKeyedServices("bff")] WebApplicationBuilder webApplicationBuilder,
    RootContainer rootContainer,
    IEnumerable<ILoggerProvider> loggerProvidersFromHost
    ) : IHostedService, IDisposable, IAsyncDisposable
{
    internal WebApplication App { get; private set; } = null!;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        webApplicationBuilder.Services.AddSingleton(rootContainer);
        webApplicationBuilder.Logging.ClearProviders();
        foreach (var loggerProvider in loggerProvidersFromHost)
        {
            webApplicationBuilder.Logging.AddProvider(loggerProvider);
        }

        App = webApplicationBuilder.Build();

        if (!App.Environment.IsDevelopment())
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            App.UseHsts();
        }

        App.UseHttpsRedirection();
        App.UseRouting();

        App.UseBff();

        foreach (var webAppConfigurator in App.Services.GetServices<ConfigureWebApp>())
        {
            webAppConfigurator.Invoke(App);
        }

        await App.StartAsync(cancellationToken);
    }

    private static WebApplicationBuilder CreateDefaultBuilder(IHostEnvironment environment)
    {
        var webApplicationOptions = new WebApplicationOptions
        {
            EnvironmentName = environment.EnvironmentName,
            ApplicationName = environment.ApplicationName,
        };

        var builder = WebApplication.CreateSlimBuilder(webApplicationOptions);

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddEnvironmentVariables("DOTNET_");
        builder.Configuration.AddEnvironmentVariables("ASPNETCORE_");

        builder.WebHost.UseKestrelHttpsConfiguration();
        // Todo Urls

        return builder;
    }

    internal IServiceProvider Services => App.Services;

    public async Task StopAsync(CancellationToken cancellationToken) => await App.StopAsync(cancellationToken);

    public void Dispose() => ((IDisposable)App).Dispose();

    public async ValueTask DisposeAsync() => await (App?.DisposeAsync() ?? ValueTask.CompletedTask);

    public class HostDependencies(
        IUserSessionStore? userSessionStore = null,
        TimeProvider? timeProvider = null
        )
    {
        public void Register(IServiceCollection services)
        {
            if (userSessionStore != null)
            {
                services.AddSingleton(userSessionStore);
            }
            if (timeProvider != null)
            {
                services.AddSingleton(timeProvider);
            }
        }
    }

}

/// <summary>
/// Defines how to create a webapplication builder for the BFF application.
/// </summary>
internal delegate void ConfigureWebAppBuilder(BffApplicationPartType partType, IHostApplicationBuilder builder);

/// <summary>
/// Describes the parts that a BFF application can consist of. 
/// </summary>
public enum BffApplicationPartType
{
    BffEndpoint,
    SessionCleanupHost
}
