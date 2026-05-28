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
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task PersistedGrantsRemovedAsync(IEnumerable<PersistedGrant> persistedGrants, Ct ct);

    /// <summary>
    /// Notification for device codes being removed.
    /// </summary>
    /// <param name="deviceCodes">The device codes being removed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task DeviceCodesRemovedAsync(IEnumerable<DeviceFlowCodes> deviceCodes, Ct ct);

    /// <summary>
    /// Notification for SAML signin states being removed.
    /// </summary>
    /// <param name="samlSigninStates">The SAML signin states being removed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task SamlSigninStatesRemovedAsync(IEnumerable<SamlSigninState> samlSigninStates, Ct ct) => Task.CompletedTask;

    /// <summary>
    /// Notification for SAML logout sessions being removed.
    /// </summary>
    /// <param name="samlLogoutSessions">The SAML logout sessions being removed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task SamlLogoutSessionsRemovedAsync(IEnumerable<SamlLogoutSession> samlLogoutSessions, Ct ct) => Task.CompletedTask;
}
