// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Implements refresh token creation and validation
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Validates a refresh token.
    /// </summary>
    /// <param name="token">The refresh token.</param>
    /// <param name="client">The client.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<TokenValidationResult> ValidateRefreshTokenAsync(string token, Client client, Ct ct);

    /// <summary>
    /// Creates the refresh token.
    /// </summary>
    /// <param name="request">The refresh token creation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The refresh token handle
    /// </returns>
    Task<string> CreateRefreshTokenAsync(RefreshTokenCreationRequest request, Ct ct);

    /// <summary>
    /// Updates the refresh token.
    /// </summary>
    /// <param name="request">The refresh token update request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The refresh token handle
    /// </returns>
    Task<string> UpdateRefreshTokenAsync(RefreshTokenUpdateRequest request, Ct ct);
}
