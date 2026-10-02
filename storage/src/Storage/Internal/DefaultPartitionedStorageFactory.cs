// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Internal;

/// <summary>
/// Default implementation of the store factory that will resolve the store from the DI container.
/// It will ALWAYS return a store for the default pool id when used through the public
/// <see cref="IPartitionedStorageFactory"/> API.
/// </summary>
/// <param name="services">The service provider used to resolve keyed storage engines.</param>
/// <param name="storageInstanceRouter">Resolves the storage instance for a given category.</param>
internal class DefaultPartitionedStorageFactory(IServiceProvider services, IStorageInstanceRouter storageInstanceRouter)
    : IPartitionedStorageFactory
{
    public Task<IPartitionedStorage> GetPartitionedStorageAsync(DataCategoryName dataCategory, Ct ct) =>
        Task.FromResult(GetPartitionedStorage(dataCategory, PoolId.Default));

    /// <summary>
    /// Resolves the keyed storage engine for the given data category and scopes it to the given pool.
    /// </summary>
    /// <remarks>
    /// The keyed <see cref="IPartitionedStorage"/> engine starts scoped to the management pool until
    /// <see cref="IPartitionedStorage.SetPoolId(PoolId)"/> is called on it. Callers that need storage scoped
    /// to a specific pool (such as Spaces) should go through this factory rather than resolving the keyed
    /// engine directly.
    /// </remarks>
    /// <param name="dataCategory">The data category to resolve a storage instance for.</param>
    /// <param name="poolId">The pool the returned storage should be scoped to.</param>
    /// <returns>An <see cref="IPartitionedStorage"/> scoped to <paramref name="poolId"/>.</returns>
    internal IPartitionedStorage GetPartitionedStorage(DataCategoryName dataCategory, PoolId poolId)
    {
        var storageInstanceId = storageInstanceRouter.Resolve(dataCategory);
        var storage = services.GetKeyedService<IPartitionedStorage>(storageInstanceId) ??
                      throw new InvalidOperationException(
                          $"Storage category '{dataCategory}' resolves to storage instance '{storageInstanceId}', but no database provider " +
                          $"is registered for that instance. Register one for '{storageInstanceId}' by calling a provider registration " +
                          "method such as AddSqlite(), AddPostgreSql(), AddMsSql(), or AddOracle() " +
                          "on that instance's storage builder.");

        storage.SetPoolId(poolId);
        return storage;
    }
}
