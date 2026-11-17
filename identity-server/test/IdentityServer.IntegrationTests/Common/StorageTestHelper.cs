// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Common;

/// <summary>
/// Shared helper for configuring SQLite-backed operational stores in protocol-level
/// integration tests.
/// </summary>
internal static class StorageTestHelper
{
    /// <summary>
    /// Configures a unique in-memory SQLite database for operational stores,
    /// leaving configuration stores unchanged and disabling background storage purge.
    /// </summary>
    public static void AddOperationalStorageForTesting(this IServiceCollection services)
    {
        var dbName = $"protocol_{Guid.NewGuid():N}";

        services.AddIdentityServerBuilder()
            .AddStorage(storage =>
                storage.AddSqlite(opt =>
                    opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
            .AddOperationalStorage();

        services.Configure<IdentityServerOptions>(o => o.StoragePurge.EnablePurge = false);
    }

    /// <summary>
    /// Creates a scoped <see cref="IStorageInstanceSchema"/> from the pipeline and
    /// runs schema migration. Call after <see cref="IdentityServerPipeline.Initialize"/>.
    /// </summary>
    public static async Task MigrateStorageSchemaAsync(this IdentityServerPipeline pipeline)
    {
        await using var scope = pipeline.ApplicationServices.CreateAsyncScope();
        var schema = scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }
}
