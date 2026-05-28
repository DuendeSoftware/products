// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves authorization codes used in the OAuth 2.0 Authorization Code
/// flow. After the user authenticates and consents, IdentityServer issues a short-lived
/// authorization code to the client. The client then exchanges this code for tokens at
/// the token endpoint. Authorization codes are single-use: they are removed from the
/// store when redeemed. The default implementation is backed by
/// <see cref="IPersistedGrantStore"/>.
/// </summary>
public interface IAuthorizationCodeStore
{
    /// <summary>
    /// Persists a new authorization code and returns the opaque code value that
    /// identifies it. The code is sent to the client via the redirect URI and is
    /// later presented at the token endpoint.
    /// </summary>
    /// <param name="code">The authorization code data to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The opaque authorization code string sent to the client via the redirect URI.
    /// </returns>
    Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code, Ct ct);

    /// <summary>
    /// Retrieves the authorization code associated with the specified code value.
    /// </summary>
    /// <param name="code">The opaque authorization code value to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="AuthorizationCode"/> associated with the specified
    /// <paramref name="code"/>, or <see langword="null"/> if no matching code exists
    /// or the code has expired.
    /// </returns>
    Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code, Ct ct);

    /// <summary>
    /// Removes the authorization code identified by the specified code value. This is
    /// called after the code has been successfully exchanged for tokens to enforce
    /// single-use semantics.
    /// </summary>
    /// <param name="code">The opaque authorization code value to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveAuthorizationCodeAsync(string code, Ct ct);
}
