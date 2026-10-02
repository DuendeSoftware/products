// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Factory for obtaining an <see cref="ICrossPartitionStorage"/> for a specific storage instance.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface ICrossPartitionStorageFactory
{
    /// <summary>
    /// Gets the <see cref="ICrossPartitionStorage"/> for the specified storage instance.
    /// </summary>
    /// <param name="storageInstanceId">The storage instance.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct);
}
