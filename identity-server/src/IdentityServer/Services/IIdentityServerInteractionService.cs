// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
///  Provide services be used by the user interface to communicate with IdentityServer.
/// </summary>
public interface IIdentityServerInteractionService
{
    /// <summary>
    /// Gets the authorization context.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AuthorizationRequest?> GetAuthorizationContextAsync(string? returnUrl, CT ct);

    /// <summary>
    /// Indicates if the returnUrl is a valid URL for redirect after login or consent.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    bool IsValidReturnUrl(string? returnUrl);

    /// <summary>
    /// Gets the error context.
    /// </summary>
    /// <param name="errorId">The error identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<ErrorMessage?> GetErrorContextAsync(string? errorId, CT ct);

    /// <summary>
    /// Gets the logout context.
    /// </summary>
    /// <param name="logoutId">The logout identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<LogoutRequest> GetLogoutContextAsync(string? logoutId, CT ct);

    /// <summary>
    /// Used to create a logoutId if there is not one presently.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string?> CreateLogoutContextAsync(CT ct);

    /// <summary>
    /// Informs IdentityServer of the user's consent.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="consent">The consent.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="subject">The subject.</param>
    Task GrantConsentAsync(AuthorizationRequest request, ConsentResponse consent, CT ct, string? subject = null);

    /// <summary>
    /// Triggers error back to the client for the authorization request.
    /// This API is a simpler helper on top of GrantConsentAsync.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="error"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="errorDescription"></param>
    Task DenyAuthorizationAsync(AuthorizationRequest request, AuthorizationError error, CT ct, string? errorDescription = null);

    /// <summary>
    /// Returns a collection representing all of the user's consents and grants.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task<IEnumerable<Grant>> GetAllUserGrantsAsync(CT ct);

    /// <summary>
    /// Revokes all a user's consents and grants for a given client, or for all clients if clientId is null.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RevokeUserConsentAsync(string? clientId, CT ct);

    /// <summary>
    /// Revokes all of a user's consents and grants for clients the user has signed into during their current session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task RevokeTokensForCurrentSessionAsync(CT ct);
}
