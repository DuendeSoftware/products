// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Internal;

internal class PooledStore(IServiceProvider serviceProvider, object? serviceKey) : IPooledStore
{
    public IStore OpenPool(PoolId poolId)
    {
        var store = serviceKey is null
            ? serviceProvider.GetRequiredService<IStore>()
            : serviceProvider.GetRequiredKeyedService<IStore>(serviceKey);
        store.SetPoolId(poolId);
        return store;
    }

    public Task<CheckSchemaVersionResult> CheckVersionAsync(Ct ct)
    {
        var databaseSchema = serviceKey is null
            ? serviceProvider.GetRequiredService<IDatabaseSchema>()
            : serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(serviceKey);
        return databaseSchema.CheckVersionAsync(ct);
    }

    public Task MigrateAsync(Ct ct)
    {
        var databaseSchema = serviceKey is null
            ? serviceProvider.GetRequiredService<IDatabaseSchema>()
            : serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(serviceKey);
        return databaseSchema.MigrateAsync(ct);
    }

    public Task<SchemaVerificationResult> VerifySchemaAsync(Ct ct)
    {
        var databaseSchema = serviceKey is null
            ? serviceProvider.GetRequiredService<IDatabaseSchema>()
            : serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(serviceKey);
        return databaseSchema.VerifySchemaAsync(ct);
    }

    public string BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var databaseSchema = serviceKey is null
            ? serviceProvider.GetRequiredService<IDatabaseSchema>()
            : serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(serviceKey);
        return databaseSchema.BuildMigrationScript(fromVersion);
    }
}
