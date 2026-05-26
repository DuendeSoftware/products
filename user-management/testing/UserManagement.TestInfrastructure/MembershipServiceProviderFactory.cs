// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public sealed class MembershipServiceProviderFactory
{
    public static async Task<ServiceProvider> CreateAsync()
    {
        var services = new ServiceCollection();

        var dbId = Guid.NewGuid();
        _ = services.AddLogging();
        _ = services.AddUserManagementInternal(users =>
        {
            _ = users.AddSqliteStore(opt => opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared");
            _ = users.EnableMembership();
        });

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IPooledStore>().MigrateAsync(CancellationToken.None);
        return sp;
    }
}
