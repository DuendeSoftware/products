// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Internal;

/// <summary>
/// Default implementation of <see cref="ICrossPartitionStorageFactory"/> that resolves the storage
/// instance directly from the DI container.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve storage instances.</param>
internal class CrossPartitionStorageFactory(IServiceProvider serviceProvider) : ICrossPartitionStorageFactory
{
    /// <inheritdoc />
    public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct) =>
        Task.FromResult(serviceProvider.GetRequiredKeyedService<ICrossPartitionStorage>(storageInstanceId));
}
