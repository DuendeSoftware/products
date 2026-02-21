// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Custom type for ITicketStore
/// </summary>
// This is here really just to avoid possible confusion of any other ITicketStore already in
// the DI system, and add a new higher level helper APIs.
public interface IServerSideTicketStore : ITicketStore
{
    /// <summary>
    /// Gets sessions for a specific subject id and/or session id
    /// </summary>
    /// <param name="filter">The session filter.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, CT ct);

    /// <summary>
    /// Queries user sessions based on filter
    /// </summary>
    /// <param name="filter">The session query filter.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter, CT ct);

    /// <summary>
    /// Removes and returns expired sessions
    /// </summary>
    /// <param name="count">The maximum number of sessions to return.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, CT ct);
}
