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
    Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties);

    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    Task<ClaimsPrincipal?> GetUserAsync();

    /// <summary>
    /// Gets the current session identifier.
    /// </summary>
    /// <returns></returns>
    Task<string?> GetSessionIdAsync();

    /// <summary>
    /// Ensures the session identifier cookie asynchronously.
    /// </summary>
    /// <returns></returns>
    Task EnsureSessionIdCookieAsync();

    /// <summary>
    /// Removes the session identifier cookie.
    /// </summary>
    Task RemoveSessionIdCookieAsync();

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <returns></returns>
    Task AddClientIdAsync(string clientId);

    /// <summary>
    /// Gets the list of clients the user has signed into during their session.
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<string>> GetClientListAsync();
}
