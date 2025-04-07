// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Duende.Bff.SessionManagement.TicketStore;

/// <summary>
/// Extends ITicketStore with additional query APIs.
/// </summary>
public interface IServerTicketStore : ITicketStore
{
    /// <summary>
    /// Returns the AuthenticationTickets for the UserSessionsFilter.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task<IReadOnlyCollection<AuthenticationTicket>> GetUserTicketsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default);
}
