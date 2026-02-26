// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Saml.Models;
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
    Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties, Ct ct);

    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task<ClaimsPrincipal?> GetUserAsync(Ct ct);

    /// <summary>
    /// Gets the current session identifier.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string?> GetSessionIdAsync(Ct ct);

    /// <summary>
    /// Ensures the session identifier cookie asynchronously.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task EnsureSessionIdCookieAsync(Ct ct);

    /// <summary>
    /// Removes the session identifier cookie.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveSessionIdCookieAsync(Ct ct);

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task AddClientIdAsync(string clientId, Ct ct);

    /// <summary>
    /// Gets the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IEnumerable<string>> GetClientListAsync(Ct ct);

    /// <summary>
    /// Adds a SAML SP session to the user's session.
    /// </summary>
    /// <param name="session">The SAML session data.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <remarks>
    /// Session data is stored in AuthenticationProperties. For deployments with many SAML service providers,
    /// server-side sessions should be enabled to avoid cookie size limitations.
    /// See <see cref="SamlSpSessionData"/> for details.
    /// </remarks>
    Task AddSamlSessionAsync(SamlSpSessionData session, Ct ct);

    /// <summary>
    /// Gets the list of SAML SP sessions for the user's session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task<IEnumerable<SamlSpSessionData>> GetSamlSessionListAsync(Ct ct);

    /// <summary>
    /// Removes a SAML SP session by EntityId.
    /// </summary>
    /// <param name="entityId">The SP's entity ID.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveSamlSessionAsync(string entityId, Ct ct);
}
