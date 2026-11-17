// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Spaces.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Registers the Storage-backed <see cref="IDistributedLock"/>. Called by <c>AddStorage</c> (default
/// storage instance only) and <c>AddSpaces</c> (always), so it must be idempotent and safe in any call
/// order relative to <c>AddIdentityServer()</c>'s in-process default registration (a <c>TryAddSingleton</c>).
/// </summary>
internal static class DistributedLockServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default in-process <see cref="IDistributedLock"/> with <see cref="StorageDistributedLock"/>,
    /// and registers the storage it needs. A no-op when a <see cref="StorageDistributedLock"/> registration
    /// already exists, so repeated calls (from multiple <c>AddStorage</c>/<c>AddSpaces</c> calls, in any order)
    /// register it exactly once. Uses only <c>Add</c>/<c>TryAdd</c>, never an <c>ImplementationFactory</c>.
    /// </summary>
    internal static void AddDistributedStorageLock(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IDistributedLock) && d.ImplementationType == typeof(StorageDistributedLock)))
        {
            return;
        }

        // Plain Add (not TryAdd): this must win over the in-process default that AddIdentityServer()
        // registers with TryAddSingleton, regardless of call order. Registered by type, so the
        // container owns and disposes the single instance.
        services.AddSingleton<IDistributedLock, StorageDistributedLock>();
        services.AddDsoRegistration<DistributedLockDso.V1>();
        services.TryAddTransient<IDistributedLockStorage, DefaultDistributedLockStorage>();
        services.TryAddTransient<DefaultPartitionedStorageFactory>();
        services.TryAddTransient<ManagementStorageAccessor>();
    }
}
