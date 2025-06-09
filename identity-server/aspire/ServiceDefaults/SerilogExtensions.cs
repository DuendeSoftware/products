// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;

namespace Microsoft.Extensions.Hosting;

public static class SerilogExtensions
{
    public static void ConfigureSerilogDefaults(this WebApplicationBuilder builder) =>
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Debug)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
                .WriteTo.OpenTelemetry(opts =>
                {
                    opts.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
                    opts.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName,
                    };
                });
        });
}

public static class SerilogDefaults
{
    public static void Bootstrap() => Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
        .CreateBootstrapLogger();
}
