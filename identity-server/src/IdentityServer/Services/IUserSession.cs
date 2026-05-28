// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Manages the current user's authentication session and tracks the client applications
/// that are participating in it. The session is identified by a unique random session ID
/// assigned when the user first logs in. As clients request tokens, their IDs are recorded
/// in the session so that IdentityServer can send logout notifications to all participating
/// clients at sign-out time.
/// This interface also exposes methods for managing the session ID cookie used by
/// IdentityServer's OIDC session management implementation.
/// The default implementation is <c>DefaultUserSession</c>, which stores the session ID
/// and client list in the authentication properties.
/// </summary>
public interface IUserSession
{
    /// <summary>
    /// Creates a new session identifier for the sign-in context and issues the session ID cookie.
    /// Call this when a user successfully authenticates to establish their session.
    /// </summary>
    /// <param name="principal">The authenticated user's claims principal.</param>
    /// <param name="properties">The authentication properties associated with the sign-in.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The newly created session ID string that uniquely identifies this user's session.
    /// </returns>
    Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties, Ct ct);

    /// <summary>
    /// Gets the currently authenticated user.
    /// Prefer this over <c>IAuthenticationService.AuthenticateAsync</c> because it avoids
    /// running claims transformation more than once and reflects any updated authentication
    /// ticket issued during the current request.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="ClaimsPrincipal"/> of the authenticated user,
    /// or <c>null</c> if no user is currently authenticated.
    /// </returns>
    Task<ClaimsPrincipal?> GetUserAsync(Ct ct);

    /// <summary>
    /// Gets the current session identifier from the authentication ticket.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The session ID string for the current user's session,
    /// or <c>null</c> if there is no active authenticated session.
    /// </returns>
    Task<string?> GetSessionIdAsync(Ct ct);

    /// <summary>
    /// Ensures the session ID cookie is present and synchronized with the current session identifier.
    /// Call this to keep the session cookie in sync after the authentication ticket has been updated.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task EnsureSessionIdCookieAsync(Ct ct);

    /// <summary>
    /// Removes the session ID cookie from the response.
    /// Call this during sign-out to clear the OIDC session management cookie.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveSessionIdCookieAsync(Ct ct);

    /// <summary>
    /// Records that the specified client has participated in the current user's session.
    /// This information is used at sign-out time to send logout notifications to all
    /// clients that were active during the session.
    /// </summary>
    /// <param name="clientId">The identifier of the client to add to the session's client list.</param>
    /// <param name="ct">The cancellation token.</param>
    Task AddClientIdAsync(string clientId, Ct ct);

    /// <summary>
    /// Gets the list of client IDs that have participated in the current user's session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of client ID strings representing every client that has
    /// obtained tokens during the current session.
    /// </returns>
    Task<IReadOnlyCollection<string>> GetClientListAsync(Ct ct);

    /// <summary>
    /// Adds a SAML SP session to the user's session, recording that the specified
    /// service provider is participating in the current SSO session.
    /// </summary>
    /// <param name="session">The SAML SP session data to record, including the SP's entity ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <remarks>
    /// Session data is stored in AuthenticationProperties. For deployments with many SAML service providers,
    /// server-side sessions should be enabled to avoid cookie size limitations.
    /// See <see cref="SamlSpSessionData"/> for details.
    /// </remarks>
    Task AddSamlSessionAsync(SamlSpSessionData session, Ct ct);

    /// <summary>
    /// Gets the list of SAML SP sessions that are participating in the current user's session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="SamlSpSessionData"/> objects representing every
    /// SAML service provider that has participated in the current session.
    /// </returns>
    Task<IReadOnlyCollection<SamlSpSessionData>> GetSamlSessionListAsync(Ct ct);

    /// <summary>
    /// Removes the SAML SP session for the specified entity ID from the current user's session.
    /// Call this during SAML single logout to deregister the service provider from the session.
    /// </summary>
    /// <param name="entityId">The entity ID of the SAML service provider whose session should be removed.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveSamlSessionAsync(string entityId, Ct ct);
}
