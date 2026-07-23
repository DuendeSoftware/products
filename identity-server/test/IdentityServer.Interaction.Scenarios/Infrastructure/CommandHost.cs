// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using IAsyncDisposable = System.IAsyncDisposable;

namespace Duende.IdentityServer.Interaction.Infrastructure;

/// <summary>
/// Context object provided to command handlers. Exposes an instrumented
/// <see cref="IHttpClientFactory"/> so commands produce proper distributed traces.
/// </summary>
public sealed class CommandContext
{
    /// <summary>
    /// An <see cref="IHttpClientFactory"/> with OpenTelemetry HttpClient instrumentation.
    /// Use this instead of <c>new HttpClient()</c> so traces propagate correctly.
    /// </summary>
    public required IHttpClientFactory HttpClientFactory { get; init; }
}

/// <summary>
/// A singleton host that provides an instrumented environment for executing scenario commands.
/// Commands run inside this host's async context, so the OTel TracerProvider is active and
/// HttpClient calls propagate trace context (traceparent) to downstream services.
/// </summary>
internal sealed class CommandHost : IAsyncDisposable
{
    public static CommandHost? Instance { get; private set; }
    private static readonly SemaphoreSlim _commandHostLock = new(1, 1);

    public static async Task<CommandHost> GetOrCreateCommandHostAsync(
        IDistributedApplicationBuilder appBuilder,
        ExecuteCommandContext context)
    {
        if (Instance != null)
        {
            return Instance;
        }

        await _commandHostLock.WaitAsync();
        try
        {
            if (Instance != null)
            {
                return Instance;
            }

            var loggerService = context.ServiceProvider.GetRequiredService<ResourceLoggerService>();
            // Use a dummy resource logger — the CommandHost is shared across all scenarios
            var logger = loggerService.GetLogger(context.ResourceName);

            var otlpEndpoint = appBuilder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"];
            var apiKey = appBuilder.Configuration["AppHost:OtlpApiKey"];
            var headers = !string.IsNullOrWhiteSpace(apiKey) ? $"x-otlp-api-key={apiKey}" : null;

            Instance = await CommandHost.CreateAsync(logger, otlpEndpoint, headers);
            return Instance;
        }
        finally
        {
            _commandHostLock.Release();
        }
    }

    public static readonly ActivitySource CommandActivitySource = new(
        "Duende.IdentityServer.Interaction.Commands", "1.0.0");

    private WebApplication? _app;
    public CommandContext Context { get; private set; } = null!;
    private Processor _processor = null!;

    public static async Task<CommandHost> CreateAsync(
        ILogger logger,
        string? otlpEndpoint,
        string? otlpHeaders)
    {
        var host = new CommandHost();
        await host.StartAsync(logger, otlpEndpoint, otlpHeaders);
        return host;
    }

    private async Task StartAsync(ILogger logger, string? otlpEndpoint, string? otlpHeaders)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls("https://127.0.0.1:0");

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ResourceLoggerProvider(logger));
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<Processor>();
        builder.Services.AddHostedService<Processor>(sp => sp.GetRequiredService<Processor>());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            var endpoint = new Uri(otlpEndpoint);

            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("scenario-commands"))
                    .AddSource(CommandActivitySource.Name)
                    .AddSource(IdentityServerConstants.Tracing.Basic)
                    .AddSource(IdentityServerConstants.Tracing.Cache)
                    .AddSource(IdentityServerConstants.Tracing.Services)
                    .AddSource(IdentityServerConstants.Tracing.Stores)
                    .AddSource(IdentityServerConstants.Tracing.Validation)
                    .AddSource("Duende.AspNetCore.Authentication.JwtBearer")
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
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("scenario-commands"))
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = endpoint;
                        if (otlpHeaders != null)
                        {
                            o.Headers = otlpHeaders;
                        }
                    }));

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("scenario-commands"));
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

        _app = builder.Build();
        await _app.StartAsync();
        _processor = _app.Services.GetRequiredService<Processor>();
        Context = new CommandContext
        {
            HttpClientFactory = _app.Services.GetRequiredService<IHttpClientFactory>()
        };
    }

    /// <summary>
    /// Executes a command within the host's instrumented context.
    /// Wraps the entire execution in an Activity for end-to-end tracing.
    /// </summary>
    public async Task<ExecuteCommandResult> ExecuteAsync(
        string commandName,
        Func<CommandContext, Task<ExecuteCommandResult>> handler) =>
        await _processor.ExecuteWork(commandName, _ => handler(Context), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }
}


public class Processor : BackgroundService
{
    public delegate Task<ExecuteCommandResult> WorkDelegate(CancellationToken ct);


    private AsyncQueue<WorkItem> _queue = new AsyncQueue<WorkItem>();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(ct);
            // process work item
            await workItem.StartWork(ct);
        }

    }

    public Task<ExecuteCommandResult> ExecuteWork(string name, WorkDelegate work, CancellationToken ct)
    {
        var workItem = new WorkItem(work, name);
        _queue.Enqueue(workItem);
        return workItem.WaitUntilWorkIsComplete(ct);
    }
}

public class WorkItem(Processor.WorkDelegate work, string name)
{
    private TaskCompletionSource<ExecuteCommandResult> _workComplete = new TaskCompletionSource<ExecuteCommandResult>();

    public async Task<ExecuteCommandResult> WaitUntilWorkIsComplete(CancellationToken ct) =>
        await _workComplete.Task;

    public async Task StartWork(CancellationToken ct)
    {
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                ExecuteCommandResult result;

                try
                {
                    using var activity = CommandHost.CommandActivitySource.StartActivity(name);
                    result = await work(ct);
                }
                catch (Exception e)
                {
                    result = CommandResults.Failure(e);
                }
                _workComplete.SetResult(result);
            }, ct);

        }
        await _workComplete.Task;
    }
}

