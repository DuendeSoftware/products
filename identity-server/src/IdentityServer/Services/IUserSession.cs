// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Models a user's authentication session
/// </summary>
public interface IUserSession
{
    /// <summary>
    /// Creates a session identifier for the signin context and issues the session id cookie.
    /// </summary>
    /// <param name="principal"></param>
    /// <param name="properties"></param>
    /// <param name="ct">The cancellation token.</param>
    Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties, CT ct);

    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task<ClaimsPrincipal?> GetUserAsync(CT ct);

    /// <summary>
    /// Gets the current session identifier.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string?> GetSessionIdAsync(CT ct);

    /// <summary>
    /// Ensures the session identifier cookie asynchronously.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task EnsureSessionIdCookieAsync(CT ct);

    /// <summary>
    /// Removes the session identifier cookie.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveSessionIdCookieAsync(CT ct);

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task AddClientIdAsync(string clientId, CT ct);

    /// <summary>
    /// Gets the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IEnumerable<string>> GetClientListAsync(CT ct);
}
