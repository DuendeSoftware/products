// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Provides administrative features for querying and terminating server-side sessions.
/// When server-side sessions are enabled, this service can be used to enumerate active sessions
/// and to terminate them — including revoking associated tokens and consents, and triggering
/// back-channel logout notifications to participating clients.
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Queries server-side session data for users, returning paged results based on the optional filter.
    /// Use this to build administrative UIs that list active sessions.
    /// </summary>
    /// <param name="filter">
    /// An optional <see cref="SessionQuery"/> to filter results by subject ID, session ID, or other criteria.
    /// Pass <c>null</c> to return all sessions.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult{UserSession}"/> containing the matching <see cref="UserSession"/> records
    /// and pagination metadata.
    /// </returns>
    Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery? filter, Ct ct);

    /// <summary>
    /// Removes server-side session data for the user(s) identified by the given context,
    /// and optionally revokes tokens, revokes consents, and sends back-channel logout notifications
    /// to the clients that participated in the session.
    /// </summary>
    /// <param name="context">
    /// A <see cref="RemoveSessionsContext"/> specifying the subject ID and/or session ID to target,
    /// the client IDs to act on, and flags controlling which actions to perform
    /// (remove session, revoke tokens, revoke consents, send back-channel logout).
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveSessionsAsync(RemoveSessionsContext context, Ct ct);
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
    public IReadOnlyCollection<string>? ClientIds { get; set; }

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
