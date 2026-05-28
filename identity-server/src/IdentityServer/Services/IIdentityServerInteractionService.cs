// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Provides services used by the user interface to communicate with IdentityServer,
/// mainly pertaining to user interaction such as login, consent, logout, and error handling.
/// This service is available from the dependency injection system and is typically injected
/// as a constructor parameter into MVC controllers that implement the IdentityServer UI.
/// </summary>
public interface IIdentityServerInteractionService
{
    /// <summary>
    /// Gets the protocol-agnostic authentication context for the current request.
    /// Returns an <see cref="AuthorizationRequest"/> for OIDC flows or a
    /// SAML authentication request for SAML flows,
    /// both behind the common <see cref="IAuthenticationContext"/> interface.
    /// Use pattern matching to access protocol-specific details.
    /// </summary>
    /// <param name="returnUrl">The return URL passed to the login or consent page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The protocol-agnostic authentication context for the request identified by <paramref name="returnUrl"/>,
    /// or <c>null</c> if the URL does not correspond to a valid pending authorization request.
    /// </returns>
    Task<IAuthenticationContext?> GetAuthenticationContextAsync(string? returnUrl, Ct ct);

    /// <summary>
    /// Returns the <see cref="AuthorizationRequest"/> based on the <paramref name="returnUrl"/> passed to the login or consent pages.
    /// Use this to obtain details about the client, requested scopes, and other OIDC parameters
    /// so the UI can tailor the login or consent experience.
    /// </summary>
    /// <param name="returnUrl">The return URL passed to the login or consent page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="AuthorizationRequest"/> describing the pending OIDC authorization request,
    /// or <c>null</c> if the URL does not correspond to a valid pending request.
    /// </returns>
    Task<AuthorizationRequest?> GetAuthorizationContextAsync(string? returnUrl, Ct ct);

    /// <summary>
    /// Indicates whether the <paramref name="returnUrl"/> is a valid URL for redirect after login or consent.
    /// Use this to guard against open-redirect attacks before trusting a return URL.
    /// </summary>
    /// <param name="returnUrl">The return URL to validate.</param>
    /// <returns><c>true</c> if the URL is a recognized and safe IdentityServer return URL; otherwise <c>false</c>.</returns>
    bool IsValidReturnUrl(string? returnUrl);

    /// <summary>
    /// Returns the <see cref="ErrorMessage"/> based on the <paramref name="errorId"/> passed to the error page.
    /// Use this to retrieve the error details so the UI can display a meaningful error to the user.
    /// </summary>
    /// <param name="errorId">The error identifier passed as a query parameter to the error page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="ErrorMessage"/> containing the error code, description, and request context,
    /// or <c>null</c> if no error matching <paramref name="errorId"/> is found.
    /// </returns>
    Task<ErrorMessage?> GetErrorContextAsync(string? errorId, Ct ct);

    /// <summary>
    /// Returns the <see cref="LogoutRequest"/> based on the <paramref name="logoutId"/> passed to the logout page.
    /// Use this to retrieve the logout context so the UI can render the sign-out prompt and
    /// the post-logout redirect URI.
    /// </summary>
    /// <param name="logoutId">The logout identifier passed as a query parameter to the logout page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="LogoutRequest"/> describing the pending logout, including the initiating client,
    /// post-logout redirect URI, session ID, and sign-out iframe URL.
    /// </returns>
    Task<LogoutRequest> GetLogoutContextAsync(string? logoutId, Ct ct);

    /// <summary>
    /// Creates a <c>logoutId</c> if there is not one presently.
    /// This creates a cookie capturing all the current state needed for sign-out, and the returned
    /// <c>logoutId</c> identifies that cookie. This is typically used when there is no current
    /// <c>logoutId</c> and the logout page must capture the current user's state prior to redirecting
    /// to an external identity provider for sign-out. The newly created <c>logoutId</c> should be
    /// round-tripped to the external provider and then used on the sign-out callback page.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <c>logoutId</c> string that can be passed to <see cref="GetLogoutContextAsync"/> to retrieve
    /// the captured sign-out state, or <c>null</c> if there is no authenticated user session to capture.
    /// </returns>
    Task<string?> CreateLogoutContextAsync(Ct ct);

    /// <summary>
    /// Informs IdentityServer of the user's consent decision for a particular authorization request.
    /// Call this after the user has reviewed and accepted (or partially accepted) the requested scopes
    /// on the consent page.
    /// </summary>
    /// <param name="request">The authorization request for which consent is being granted.</param>
    /// <param name="consent">The consent response containing the scopes the user agreed to and whether to remember the decision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="subject">
    /// The subject identifier of the user granting consent. When <c>null</c>, the currently authenticated
    /// user's subject is used.
    /// </param>
    Task GrantConsentAsync(AuthorizationRequest request, ConsentResponse consent, Ct ct, string? subject = null);

    /// <summary>
    /// Sends an error back to the client for the given authorization request.
    /// This is a simpler helper on top of <see cref="GrantConsentAsync"/> for the case where
    /// the UI needs to return an OAuth/OIDC error (e.g. <c>access_denied</c>) to the client
    /// without going through the full consent flow.
    /// </summary>
    /// <param name="request">The authorization request that is being denied.</param>
    /// <param name="error">The OAuth/OIDC error code to return to the client.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="errorDescription">An optional human-readable description of the error to include in the response.</param>
    Task DenyAuthorizationAsync(AuthorizationRequest request, InteractionError error, Ct ct, string? errorDescription = null);

    /// <summary>
    /// Signals that the user has denied or cancelled the authentication request.
    /// This is protocol-agnostic — it works for both OIDC and SAML flows.
    /// For OIDC, it writes a denial to the consent store (equivalent to <see cref="DenyAuthorizationAsync"/>).
    /// For SAML, it writes a denial to the SAML signin state store, causing the callback endpoint
    /// to generate an error response back to the service provider.
    /// </summary>
    /// <param name="context">The authentication context obtained from <see cref="GetAuthenticationContextAsync"/>.</param>
    /// <param name="error">The interaction error to signal.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="errorDescription">An optional human-readable description of the error.</param>
    Task DenyAuthenticationAsync(IAuthenticationContext context, InteractionError error, Ct ct, string? errorDescription = null);

    /// <summary>
    /// Returns a collection representing all of the current user's consents and grants.
    /// Each <see cref="Grant"/> represents either a user's consent or a client's access to a user's resource.
    /// Use this to build a grants management page where users can review their authorized applications.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="Grant"/> objects for the current user,
    /// including the client, scopes, creation time, and expiration for each grant.
    /// </returns>
    Task<IReadOnlyCollection<Grant>> GetAllUserGrantsAsync(Ct ct);

    /// <summary>
    /// Revokes all of the current user's consents and grants for the specified client,
    /// or for all clients if <paramref name="clientId"/> is <c>null</c>.
    /// </summary>
    /// <param name="clientId">
    /// The identifier of the client whose grants should be revoked,
    /// or <c>null</c> to revoke grants for all clients.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task RevokeUserConsentAsync(string? clientId, Ct ct);

    /// <summary>
    /// Revokes all of the current user's consents and grants for every client
    /// the user has signed into during their current session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task RevokeTokensForCurrentSessionAsync(Ct ct);
}
