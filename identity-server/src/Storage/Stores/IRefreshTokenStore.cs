// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves refresh tokens. Refresh tokens allow clients to obtain new
/// access tokens without requiring the user to re-authenticate. IdentityServer stores
/// refresh token data server-side and issues an opaque handle to the client. This store
/// manages the full lifecycle of refresh tokens, including creation, rotation (update),
/// retrieval, and revocation. The default implementation is backed by
/// <see cref="IPersistedGrantStore"/>.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Persists a new refresh token and returns the opaque handle that identifies it.
    /// The handle is issued to the client as the refresh token parameter.
    /// </summary>
    /// <param name="refreshToken">The refresh token data to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The opaque handle string issued to the client as the refresh token parameter.
    /// </returns>
    Task<string> StoreRefreshTokenAsync(RefreshToken refreshToken, Ct ct);

    /// <summary>
    /// Replaces the stored data for an existing refresh token identified by its handle.
    /// This is called during token rotation when a new refresh token is issued in
    /// exchange for the current one.
    /// </summary>
    /// <param name="handle">The opaque handle of the refresh token to update.</param>
    /// <param name="refreshToken">The updated refresh token data to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateRefreshTokenAsync(string handle, RefreshToken refreshToken, Ct ct);

    /// <summary>
    /// Retrieves the refresh token associated with the specified handle.
    /// </summary>
    /// <param name="refreshTokenHandle">The opaque handle that identifies the refresh token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="RefreshToken"/> associated with the specified
    /// <paramref name="refreshTokenHandle"/>, or <see langword="null"/> if no matching
    /// token exists or the token has expired.
    /// </returns>
    Task<RefreshToken?> GetRefreshTokenAsync(string refreshTokenHandle, Ct ct);

    /// <summary>
    /// Removes the refresh token identified by the specified handle.
    /// </summary>
    /// <param name="refreshTokenHandle">The opaque handle of the refresh token to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveRefreshTokenAsync(string refreshTokenHandle, Ct ct);

    /// <summary>
    /// Removes all refresh tokens issued to the specified subject and client.
    /// </summary>
    /// <param name="subjectId">The subject identifier whose tokens should be removed.</param>
    /// <param name="clientId">The client identifier whose tokens should be removed.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveRefreshTokensAsync(string subjectId, string clientId, Ct ct);
}
