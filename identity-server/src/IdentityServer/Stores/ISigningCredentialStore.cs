// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Provides the active signing credentials used by IdentityServer to sign tokens such
/// as identity tokens and JWT access tokens. The returned <see cref="SigningCredentials"/>
/// represent the current primary signing key. Implement this interface to supply signing
/// credentials from a custom key management solution.
/// </summary>
public interface ISigningCredentialStore
{
    /// <summary>
    /// Gets the active signing credentials used to sign tokens.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="SigningCredentials"/> that IdentityServer uses to sign tokens.
    /// </returns>
    Task<SigningCredentials> GetSigningCredentialsAsync(Ct ct);
}
