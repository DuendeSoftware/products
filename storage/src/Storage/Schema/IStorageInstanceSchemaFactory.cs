// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Schema;

/// <summary>
/// Resolves the <see cref="IStorageInstanceSchema"/> for a storage instance. Database structure and
/// migration are instance-level concerns, independent of the ambient logical pool.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// Implementations resolve services synchronously, without performing database I/O, from the
/// caller's DI scope, and never capture a scoped dependency inside a singleton. Accessing an
/// unregistered instance fails with a clear diagnostic identifying the missing instance; there is
/// no fallback to a different, unrelated instance.
/// </remarks>
public interface IStorageInstanceSchemaFactory
{
    /// <summary>
    /// Gets the <see cref="IStorageInstanceSchema"/> for <see cref="StorageInstanceId.Default"/>, independent
    /// of any category binding or ambient logical pool.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="StorageInstanceId.Default"/> has not been registered, or no engine-backed database
    /// schema service could be resolved for it.
    /// </exception>
    Task<IStorageInstanceSchema> GetStorageInstanceSchema(CancellationToken ct) => GetStorageInstanceSchema(StorageInstanceId.Default, ct);

    /// <summary>
    /// Gets the <see cref="IStorageInstanceSchema"/> for the specified storage instance, independent of
    /// any category binding or ambient logical pool.
    /// </summary>
    /// <param name="storageInstanceId">The storage instance.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="storageInstanceId"/> has not been registered, or no engine-backed database schema
    /// service could be resolved for it.
    /// </exception>
    Task<IStorageInstanceSchema> GetStorageInstanceSchema(StorageInstanceId storageInstanceId, CancellationToken ct);
}
