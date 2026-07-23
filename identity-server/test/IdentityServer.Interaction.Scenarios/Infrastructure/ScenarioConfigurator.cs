// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Duende.IdentityServer.Interaction.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IScenarioConfigurator"/> that wires
/// OpenTelemetry and resource logging into each WebApplicationBuilder.
/// </summary>
internal sealed class ScenarioConfigurator(
    ILogger resourceLogger,
    string? otlpEndpoint,
    string? otlpHeaders) : IScenarioConfigurator
{
    public WebApplicationBuilder CreateBuilder(string serviceName)
    {
        var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        webBuilder.WebHost.UseUrls("https://127.0.0.1:0");

        // Wire console logs to Aspire dashboard's "Console Logs" tab
        webBuilder.Logging.ClearProviders();
        webBuilder.Logging.AddProvider(new ResourceLoggerProvider(resourceLogger));
        webBuilder.Logging.SetMinimumLevel(LogLevel.Debug);

        // Wire OpenTelemetry for structured logs, traces, and metrics
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            var endpoint = new Uri(otlpEndpoint);

            webBuilder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddSource(IdentityServerConstants.Tracing.Basic)
                    .AddSource(IdentityServerConstants.Tracing.Cache)
                    .AddSource(IdentityServerConstants.Tracing.Services)
                    .AddSource(IdentityServerConstants.Tracing.Stores)
                    .AddSource(IdentityServerConstants.Tracing.Validation)
                    .AddSource("Duende.AspNetCore.Authentication.JwtBearer")
                    .AddSource("Duende.IdentityServer.Interaction.Scenarios")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = endpoint;
                        if (otlpHeaders != null)
                        {
                            o.Headers = otlpHeaders;
                        }
                    }))
                .WithMetrics(metrics => metrics
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(Telemetry.ServiceName)
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = endpoint;
                        if (otlpHeaders != null)
                        {
                            o.Headers = otlpHeaders;
                        }
                    }));

            webBuilder.Logging.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName));
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    if (otlpHeaders != null)
                    {
                        o.Headers = otlpHeaders;
                    }
                });
            });
        }

        return webBuilder;
    }
}
