// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public sealed class MembershipServiceProviderFactory
{
    public static async Task<ServiceProvider> CreateAsync()
    {
        var services = new ServiceCollection();

        var dbId = Guid.NewGuid();
        _ = services
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddLogging();

        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory(dbId.ToString()));

        _ = services
            .AddUserManagementInternal(StorageInstanceId.Default, c => { });
        _ = DataCategoryNameRecorder.RegisterAsSpy(services);

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct.None);
        return sp;
    }
}
