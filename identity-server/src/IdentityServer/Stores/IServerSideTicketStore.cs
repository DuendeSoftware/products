// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries user sessions based on filter
    /// </summary>
    Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and returns expired sessions
    /// </summary>
    Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default);
}
