// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Duende.Storage.Oracle;

public sealed class AspireFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public string ServerConnectionString { get; private set; } = null!;

    /// <summary>
    /// Connection string for a dedicated schema used by the smoke tests that need
    /// a stable connection string rather than a pooled one.
    /// </summary>
    public string ConnectionString { get; private set; } = null!;

    /// <summary>
    /// Pool of reusable schemas shared across all test classes in this collection.
    /// </summary>
    internal OracleDatabasePool Pool { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        Environment.SetEnvironmentVariable("TESTAPPHOST_RESOURCES", "oracle");
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TestAppHost>(ct);
        _app = await builder.BuildAsync(ct);
        await _app.StartAsync(ct);

        // WaitForResourceHealthyAsync can hang in CI when the persistent container
        // (started by warmup) is already healthy before the test app subscribes to
        // notifications. Use a timeout and fall through — the container is ready.
        using (var healthCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            healthCts.CancelAfter(TimeSpan.FromSeconds(120));
            try
            {
                _ = await _app.ResourceNotifications.WaitForResourceHealthyAsync("oracle", healthCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout waiting for health notification — container is likely already healthy from warmup.
                Console.WriteLine("WaitForResourceHealthyAsync timed out; proceeding with connection string.");
            }
        }

        ServerConnectionString = (await _app.GetConnectionStringAsync("oracle", ct))!;

        Pool = new OracleDatabasePool(ServerConnectionString);

        // Dedicated schema for the smoke tests (OracleStoreTests) that need a
        // persistent connection string rather than a pooled one.
        ConnectionString = await Pool.GetConnectionStringAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            if (Pool != null)
            {
                await Pool.DropAllAsync();
            }

            await _app.DisposeAsync();
        }
    }
}
