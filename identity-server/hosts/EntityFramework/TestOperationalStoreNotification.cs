// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable CA1303 // Do not pass literals as localized parameters

using Duende.IdentityServer.EntityFramework;
using Duende.IdentityServer.EntityFramework.Entities;

namespace IdentityServerHost;

public class TestOperationalStoreNotification : IOperationalStoreNotification
{
    public TestOperationalStoreNotification()
    {
        Console.WriteLine("ctor");
    }

    public Task PersistedGrantsRemovedAsync(IEnumerable<PersistedGrant> persistedGrants, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistedGrants);
        foreach (var grant in persistedGrants)
        {
            Console.WriteLine("cleaned: " + grant.Type);
        }
        return Task.CompletedTask;
    }

    public Task DeviceCodesRemovedAsync(IEnumerable<DeviceFlowCodes> deviceCodes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceCodes);
        foreach (var deviceCode in deviceCodes) 
        {
            Console.WriteLine("cleaned device code");
        }
        return Task.CompletedTask;
    }

    public Task ServerSideSessionsRemovedAsync(IEnumerable<ServerSideSession> userSessions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userSessions);
        foreach (var session in userSessions)
        {
            Console.WriteLine("cleaned user session");
        }
        return Task.CompletedTask;
    }
}