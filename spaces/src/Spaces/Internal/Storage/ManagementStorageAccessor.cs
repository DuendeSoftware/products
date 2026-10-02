// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;

namespace Duende.Spaces.Internal.Storage;

internal sealed class ManagementStorageAccessor(DefaultPartitionedStorageFactory storageFactory)
{
    internal IPartitionedStorage GetManagementStorage() =>
        storageFactory.GetPartitionedStorage(DataCategoryName.Spaces, PoolId.Management);
}
