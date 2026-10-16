// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.Storage;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

/// <summary>
/// Builds the service provider for a Storage store contract test through IdentityServer's public
/// registration API, the same way a customer configures it:
/// <c>AddIdentityServer().AddStorage(...).AddConfigurationStorage()</c> (or <c>AddOperationalStorage()</c>).
/// </summary>
internal static class StorageContractTestServices
{
    /// <summary>
    /// Creates a provider backed by an in-memory SQLite database named <paramref name="dbName"/>, with the
    /// storage category recorder installed, and migrates the database.
    /// </summary>
    /// <param name="dbName">The name of the shared-cache in-memory SQLite database.</param>
    /// <param name="addStorage">Selects the IdentityServer data to store, e.g. <c>b => b.AddConfigurationStorage()</c>.</param>
    /// <param name="configureServices">Optional test-specific service overrides.</param>
    public static async Task<ServiceProvider> CreateAsync(
        string dbName,
        Action<IIdentityServerBuilder> addStorage,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqlite(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));
        addStorage(builder);

        configureServices?.Invoke(services);

        // Register last so the spy wraps the router as finally configured.
        DataCategoryNameRecorder.RegisterAsSpy(services);

        var provider = services.BuildServiceProvider();

        var ct = TestContext.Current.CancellationToken;
        var schema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>()
            .GetStorageInstanceSchema(StorageInstanceId.Default, ct);
        await schema.MigrateAsync(ct);

        return provider;
    }
}
