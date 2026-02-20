// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Session management service
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Queries all the session related data for a user.
    /// </summary>
    Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery? filter = null, CT ct = default);

    /// <summary>
    /// Removes all the session related data for a user.
    /// </summary>
    Task RemoveSessionsAsync(RemoveSessionsContext context, CT ct = default);
}

/// <summary>
/// Models the information to remove a user's session data.
/// </summary>
public class RemoveSessionsContext
{
    /// <summary>
    /// The subject ID
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// The session ID
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// The client ids for which to trigger logout notification, or revoke tokens or consent.
    /// If not set, then all clients will be removed.
    /// </summary>
    public IEnumerable<string>? ClientIds { get; set; }

    /// <summary>
    /// Removes the server side session for the user's session.
    /// </summary>
    public bool RemoveServerSideSession { get; set; } = true;

    /// <summary>
    /// Sends a back channel logout notification (if clients are registered for one).
    /// </summary>
    public bool SendBackchannelLogoutNotification { get; set; } = true;

    /// <summary>
    /// Revokes all tokens (e.g. refresh and reference) for the clients.
    /// </summary>
    public bool RevokeTokens { get; set; } = true;

    /// <summary>
    /// Revokes all prior consent granted to the clients.
    /// </summary>
    public bool RevokeConsents { get; set; } = true;
}
