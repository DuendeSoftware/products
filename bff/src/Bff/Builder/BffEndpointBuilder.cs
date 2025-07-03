// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Builder;

internal sealed class BffEndpointBuilder : IBffEndpointBuilder
{
    private WebApplicationBuilder WebAppBuilder;
    public BffEndpointBuilder(IBffApplicationBuilder bffApplicationBuilder)
    {
        BffApplicationBuilder = bffApplicationBuilder;

        var hostBuilder = bffApplicationBuilder.HostBuilder;
        WebAppBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();

        foreach (var configurator in bffApplicationBuilder.WebAppConfigurators)
        {
            configurator.Invoke(BffApplicationPartType.BffEndpoint, WebAppBuilder);
        }

        // Replace log providers with the providers from the root service provider

        var bffBuilder = new BffBuilder(Services)
            .AddBaseBffServices()
            .AddDynamicFrontends();

        bffBuilder.LoadConfiguration(hostBuilder.Configuration.GetSection("Bff"));

        var config = hostBuilder.Configuration.Get<BffHostConfiguration>() ?? new BffHostConfiguration();

        if (!config.IsEnabled(BffApplicationPartType.BffEndpoint))
        {
            return;
        }

        hostBuilder.Services.AddKeyedSingleton<WebApplicationBuilder>("bff", WebAppBuilder);
        hostBuilder.Services.AddSingleton<BffEndpointHostedService>();
        hostBuilder.Services.AddHostedService(sp => sp.GetRequiredService<BffEndpointHostedService>());

    }

    public IBffApplicationBuilder BffApplicationBuilder { get; }

    public IBffEndpointBuilder ConfigureApp(Action<WebApplication> configure)
    {
        WebAppBuilder.Services.AddSingleton<ConfigureWebApp>(webApp => configure(webApp));
        return this;
    }

    public IBffEndpointBuilder UseUrls(params string[] urls)
    {
        WebAppBuilder.WebHost.UseUrls(urls);
        return this;
    }


    public IServiceCollection Services => WebAppBuilder.Services;

}

