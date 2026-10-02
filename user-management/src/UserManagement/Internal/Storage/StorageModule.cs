// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Internal.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.UserManagement.Internal.Storage;

internal sealed class StorageModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPartitionedStorageFactory, DefaultPartitionedStorageFactory>();
    }
}
