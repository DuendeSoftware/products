// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Handles the sign-in ceremony after a successful passkey authentication.
/// Implement this interface to customize how the authenticated user is signed in
/// (e.g., to integrate with IdentityServer's session management).
/// </summary>
public interface IPasskeySignInHandler
{
    /// <summary>
    /// Signs the user in after successful passkey authentication and returns the HTTP result.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="user">The authenticated user's authenticator information.</param>
    /// <param name="userVerified">Whether the user was verified during the passkey ceremony.</param>
    /// <param name="backedUp">Whether the passkey credential is backed up.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IResult> SignInAsync(HttpContext context, UserAuthenticators user, bool userVerified, bool backedUp, Ct ct);
}
