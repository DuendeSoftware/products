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

        // Add a method that throws if no storage is registered. 
        services.TryAddSingleton<IPooledStore>(_ => throw new InvalidOperationException(
            "No storage provider has been configured. Call a storage registration method such as " +
            "AddPostgreSqlStore(), AddMsSqlStore(), or AddSqliteStore() inside the " +
            "AddUserManagementInternal configuration callback."));

        _ = services.AddSingleton<IStoreFactory, DefaultStoreFactory>();
    }
}
