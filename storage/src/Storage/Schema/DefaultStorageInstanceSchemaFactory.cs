// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Schema;

/// <summary>
/// Default <see cref="IStorageInstanceSchemaFactory"/> implementation. Resolves <see cref="IStorageInstanceSchema"/>
/// for a storage instance directly (keyed DI, independent of any ambient logical pool). Performs no
/// database I/O.
/// </summary>
/// <param name="services">
/// The service provider this factory was resolved from. The factory is registered transient, so it
/// uses the provider of whichever scope resolved it.
/// </param>
internal sealed class DefaultStorageInstanceSchemaFactory(IServiceProvider services)
    : IStorageInstanceSchemaFactory
{
    public Task<IStorageInstanceSchema> GetStorageInstanceSchema(StorageInstanceId storageInstanceId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(storageInstanceId);

        var storageInstanceSchema = services.GetKeyedService<IStorageInstanceSchema>(storageInstanceId);

        if (storageInstanceSchema == null)
        {
            throw new InvalidOperationException(
                $"No storage instance named '{storageInstanceId}' has been registered. Call a storage " +
                "registration method such as AddSqlite(), AddPostgreSql(), AddMsSql(), " +
                "or AddOracle() for this instance.");
        }

        return Task.FromResult(storageInstanceSchema);
    }
}
