// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Provides access to a user's persisted grants, which represent consents and authorizations
/// that have been granted to client applications. Use this service to retrieve or revoke
/// grants on behalf of a user, for example when building a grants management page.
/// </summary>
public interface IPersistedGrantService
{
    /// <summary>
    /// Gets all grants for the specified subject ID.
    /// Each <see cref="Grant"/> represents a consent or authorization the user has given to a client,
    /// including the client identifier, granted scopes, creation time, and expiration.
    /// </summary>
    /// <param name="subjectId">The subject identifier (user ID) whose grants should be retrieved.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="Grant"/> objects representing all active grants
    /// for the specified user.
    /// </returns>
    Task<IReadOnlyCollection<Grant>> GetAllGrantsAsync(string subjectId, Ct ct);

    /// <summary>
    /// Removes all grants for the specified subject ID, optionally scoped to a particular
    /// client and/or session. When <paramref name="clientId"/> and <paramref name="sessionId"/>
    /// are both omitted, all grants for the user are removed.
    /// </summary>
    /// <param name="subjectId">The subject identifier (user ID) whose grants should be removed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="clientId">
    /// The client identifier to restrict removal to a specific client, or <c>null</c> to remove
    /// grants for all clients.
    /// </param>
    /// <param name="sessionId">
    /// The session identifier to restrict removal to a specific session, or <c>null</c> to remove
    /// grants across all sessions.
    /// </param>
    Task RemoveAllGrantsAsync(string subjectId, Ct ct, string? clientId = null, string? sessionId = null);
}
