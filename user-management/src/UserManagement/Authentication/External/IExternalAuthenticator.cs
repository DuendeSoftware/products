// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.External;

/// <summary>
/// Resolves an external authenticator address to a user subject ID,
/// creating the user if necessary.
/// </summary>
public interface IExternalAuthenticator
{
    /// <summary>
    /// Attempts to resolve the given external authenticator address to a user subject ID.
    /// If a user with the given address already exists, returns their subject ID.
    /// If no user exists, creates a new user and returns the new subject ID.
    /// </summary>
    /// <param name="address">The external authenticator address to resolve.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="ExternalAuthenticationResult.Success"/> containing the user's subject ID,
    /// or a <see cref="ExternalAuthenticationResult.Failure"/> if the resolution failed.
    /// </returns>
    Task<ExternalAuthenticationResult> TryAuthenticateAsync(ExternalAuthenticatorAddress address, Ct ct);
}
