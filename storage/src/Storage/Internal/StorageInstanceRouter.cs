// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

internal class StorageInstanceRouter : IStorageInstanceRouter
{
    private readonly Dictionary<DataCategoryName, StorageInstanceId> _mappings = new();
    private readonly HashSet<StorageInstanceId> _registeredInstanceSet = new();
    private readonly List<StorageInstanceId> _registeredInstances = new();

    public void AddMapping(DataCategoryName dataCategory, StorageInstanceId storageInstanceId)
    {
        if (!_mappings.TryAdd(dataCategory, storageInstanceId))
        {
            throw new InvalidOperationException(
                $"Data category '{dataCategory}' cannot be mapped to {storageInstanceId} because it is already mapped to instance {_mappings[dataCategory]}");
        }
    }

    public StorageInstanceId Resolve(DataCategoryName dataCategory)
    {
        if (!_mappings.TryGetValue(dataCategory, out var storageInstanceId))
        {
            storageInstanceId = StorageInstanceId.Default;
        }

        return storageInstanceId;
    }

    public void RegisterInstance(StorageInstanceId storageInstanceId)
    {
        if (_registeredInstanceSet.Add(storageInstanceId))
        {
            _registeredInstances.Add(storageInstanceId);
        }
    }

    public IReadOnlyCollection<StorageInstanceId> GetAll() => _registeredInstances.ToArray();
}
