// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public class HostShutdownTests
{
    [Fact]
    public async Task when_stopping_the_host_synchronously_on_a_single_threaded_synchronization_context_while_a_diagnostic_summary_is_being_written_expect_the_host_to_stop()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddIdentityServer(options => options.Diagnostics.LogFrequency = TimeSpan.FromMilliseconds(10));
        builder.Services.AddSingleton<PausingSchemeProvider>();
        builder.Services.AddSingleton<IAuthenticationSchemeProvider>(services => services.GetRequiredService<PausingSchemeProvider>());
        await using var app = builder.Build();
        app.UseIdentityServer();
        var schemeProvider = app.Services.GetRequiredService<PausingSchemeProvider>();

        await app.StartAsync();
        await schemeProvider.SummaryPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // This is how WebApplicationFactory.Dispose() stops the host under xUnit v2: from a thread whose
        // SynchronizationContext only runs work on that thread, which is blocked waiting for the stop.
        var stopping = RunOnBlockedThread(() => app.StopAsync());

        schemeProvider.ResumeSummary.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task when_starting_and_stopping_the_host_synchronously_on_a_single_threaded_synchronization_context_expect_the_host_to_stop_before_the_shutdown_timeout()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddIdentityServer();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(1));
        await using var app = builder.Build();
        app.UseIdentityServer();

        // Before .NET 10, BackgroundService runs the start of ExecuteAsync on the thread that starts the host.
        // WebApplicationFactory starts the host on the same blocked thread that later stops it.
        await RunOnBlockedThread(() => app.StartAsync()).WaitAsync(TimeSpan.FromSeconds(10));
        var stopping = RunOnBlockedThread(() => app.StopAsync());

        await stopping.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static Task RunOnBlockedThread(Func<Task> action)
    {
        Task running = null!;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockedThreadContext());
            running = action();
        });
        thread.Start();
        thread.Join();
        return running;
    }

    private sealed class PausingSchemeProvider(IOptions<AuthenticationOptions> options) : AuthenticationSchemeProvider(options)
    {
        public TaskCompletionSource SummaryPaused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ResumeSummary { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
        {
            // The periodic diagnostic summary lists the schemes. Pause the first one, so it is still running when the host stops.
            if (SummaryPaused.TrySetResult())
            {
                await ResumeSummary.Task;
            }

            return await base.GetAllSchemesAsync();
        }
    }

    private sealed class BlockedThreadContext : SynchronizationContext
    {
        // The context's only thread is blocked, so work posted to it never runs.
        public override void Post(SendOrPostCallback d, object state)
        {
        }
    }
}
