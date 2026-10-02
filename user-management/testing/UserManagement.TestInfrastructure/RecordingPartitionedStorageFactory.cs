// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;

namespace Duende.Platform.UserManagement;

/// <summary>
/// Records the <see cref="DataCategoryName"/> requested on each call to <see cref="IPartitionedStorageFactory.GetPartitionedStorageAsync"/>.
/// Shared as a singleton because <see cref="IPartitionedStorageFactory"/> is registered with a transient lifetime.
/// </summary>
public sealed class DataCategoryNameRecorder
{
    private readonly List<DataCategoryName> _requestedCategories = [];

    /// <summary>
    /// The categories requested via <see cref="RecordingPartitionedStorageFactory.GetPartitionedStorageAsync"/>, in call order.
    /// </summary>
    public IReadOnlyList<DataCategoryName> RequestedCategories => _requestedCategories;

    internal void Record(DataCategoryName dataCategory) => _requestedCategories.Add(dataCategory);
}

/// <summary>
/// An <see cref="IPartitionedStorageFactory"/> decorator that records the <see cref="DataCategoryName"/>
/// requested on each call before delegating to the wrapped factory.
/// </summary>
public sealed class RecordingPartitionedStorageFactory(IPartitionedStorageFactory inner, DataCategoryNameRecorder recorder) : IPartitionedStorageFactory
{
    public Task<IPartitionedStorage> GetPartitionedStorageAsync(DataCategoryName dataCategory, CancellationToken ct)
    {
        recorder.Record(dataCategory);
        return inner.GetPartitionedStorageAsync(dataCategory, ct);
    }
}
