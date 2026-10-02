// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Resolves the <see cref="StorageInstanceId"/> that a <see cref="DataCategoryName"/> is mapped to.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IStorageInstanceRouter
{
    /// <summary>
    /// Maps a storage category to a storage instance.
    /// </summary>
    /// <param name="dataCategory">The storage category to map.</param>
    /// <param name="storageInstanceId">The storage instance to map the category to.</param>
    void AddMapping(DataCategoryName dataCategory, StorageInstanceId storageInstanceId);

    /// <summary>
    /// Resolves the storage instance mapped to the specified category, or
    /// <see cref="StorageInstanceId.Default"/> if no mapping exists.
    /// </summary>
    /// <param name="dataCategory">The storage category to resolve.</param>
    StorageInstanceId Resolve(DataCategoryName dataCategory);

    /// <summary>
    /// Registers a storage instance as actually configured (i.e. an engine has been registered for
    /// it), so it is included in <see cref="GetAll"/>. Registering the same instance more than once
    /// is a no-op.
    /// </summary>
    /// <param name="storageInstanceId">The storage instance to register.</param>
    void RegisterInstance(StorageInstanceId storageInstanceId);

    /// <summary>
    /// Gets each storage instance that has actually been registered (via <see cref="RegisterInstance"/>),
    /// exactly once. <see cref="StorageInstanceId.Default"/> is only included if it was itself registered.
    /// </summary>
    IReadOnlyCollection<StorageInstanceId> GetAll();
}
