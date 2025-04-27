// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for refresh token storage
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Stores the refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    Task<string> StoreRefreshTokenAsync(RefreshToken refreshToken);

    /// <summary>
    /// Updates the refresh token.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="refreshToken">The refresh token.</param>
    Task UpdateRefreshTokenAsync(string handle, RefreshToken refreshToken);

    /// <summary>
    /// Gets the refresh token.
    /// </summary>
    /// <param name="refreshTokenHandle">The refresh token handle.</param>
    Task<RefreshToken?> GetRefreshTokenAsync(string refreshTokenHandle);

    /// <summary>
    /// Removes the refresh token.
    /// </summary>
    /// <param name="refreshTokenHandle">The refresh token handle.</param>
    Task RemoveRefreshTokenAsync(string refreshTokenHandle);

    /// <summary>
    /// Removes the refresh tokens.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    Task RemoveRefreshTokensAsync(string subjectId, string clientId);
}
