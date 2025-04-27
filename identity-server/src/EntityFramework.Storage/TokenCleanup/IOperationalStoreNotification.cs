// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework.Entities;

namespace Duende.IdentityServer.EntityFramework;

/// <summary>
/// Interface to model notifications from the TokenCleanup feature.
/// </summary>
public interface IOperationalStoreNotification
{
    /// <summary>
    /// Notification for persisted grants being removed.
    /// </summary>
    /// <param name="persistedGrants"></param>
    /// <param name="cancellationToken"></param>
    Task PersistedGrantsRemovedAsync(IEnumerable<PersistedGrant> persistedGrants, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notification for device codes being removed.
    /// </summary>
    /// <param name="deviceCodes"></param>
    /// <param name="cancellationToken"></param>
    Task DeviceCodesRemovedAsync(IEnumerable<DeviceFlowCodes> deviceCodes, CancellationToken cancellationToken = default);
}
