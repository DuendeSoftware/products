// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

var isWarmup = args.Length > 0 && args[0] == "-warmup";

// Determine which resources to start. Sources (in priority order):
// 1. Warmup CLI args: `-warmup postgresql` or `-warmup sqlserver`
// 2. Environment variable: TESTAPPHOST_RESOURCES=postgresql or TESTAPPHOST_RESOURCES=sqlserver
// 3. Default: start all resources
var resources = isWarmup
    ? args.Skip(1).ToHashSet(StringComparer.OrdinalIgnoreCase)
    : (Environment.GetEnvironmentVariable("TESTAPPHOST_RESOURCES") is { Length: > 0 } envValue
        ? envValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
        : []);
var startAll = resources.Count == 0;

var options = new DistributedApplicationOptions
{
    Args = isWarmup ? [] : args,
    DisableDashboard = isWarmup,
};

var builder = DistributedApplication.CreateBuilder(options);

// Suppress default health check logs — they log full stack traces at Error level on every
// failed poll while containers are starting. We replace with a custom listener below that
// logs a concise info message per failed poll instead.
builder.Services.AddLogging(logging =>
{
    _ = logging.AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks", LogLevel.None);
    // Suppress noisy GSSAPI and checkpoint logs from the PostgreSQL container resource
    _ = logging.AddFilter("Duende.TestAppHost.Resources.postgresql", LogLevel.None);
});

// Use fixed passwords so the warmup step and test app agree on credentials
// when reusing persistent containers.
if (startAll || resources.Contains("sqlserver"))
{
    var sqlPassword = builder.AddParameter("sqlserver-password", "DuendeTests!1");
    _ = builder.AddSqlServer("sqlserver", sqlPassword, port: 37834)
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("duende-storage-sqlserver");
}

if (startAll || resources.Contains("postgresql"))
{
    var pgPassword = builder.AddParameter("postgresql-password", "DuendeTests!1");
    _ = builder.AddPostgres("postgresql", password: pgPassword, port: 37833)
        .WithArgs(
            "-c", "max_connections=500", // increase max connections so fast concurrent execution of tests doesn't run out of connections
            "-c", "shared_buffers=512MB", // the primary cache for data (25% of total RAM is the rule of thumb)
            "-c", "work_mem=16MB") // memory per sort/join operation (useful for complex queries)
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("duende-storage-postgresql");
}

var app = builder.Build();

if (isWarmup)
{
    await app.StartAsync();

    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Warmup");
    var notificationService = app.Services.GetRequiredService<ResourceNotificationService>();

    if (startAll || resources.Contains("sqlserver"))
    {
        await WaitForHealthyAsync("sqlserver");
    }

    if (startAll || resources.Contains("postgresql"))
    {
        await WaitForHealthyAsync("postgresql");
    }

    await app.StopAsync();

    async Task WaitForHealthyAsync(string resourceName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await foreach (var notification in notificationService.WatchAsync(cts.Token))
        {
            if (notification.Resource.Name != resourceName)
            {
                continue;
            }

            if (notification.Snapshot.HealthStatus == HealthStatus.Healthy)
            {
                logger.LogInformation("{Resource} is healthy", resourceName);
                return;
            }

            var state = notification.Snapshot.State?.Text ?? "unknown";
            logger.LogInformation("{Resource} not yet healthy (state: {State}), waiting...",
                resourceName, state);
        }
    }
}
else
{
    await app.RunAsync();
}
