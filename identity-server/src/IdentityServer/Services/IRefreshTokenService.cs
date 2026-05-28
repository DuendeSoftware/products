// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Handles the lifecycle of refresh tokens, including validation, creation, and rotation.
/// The default implementation is <c>DefaultRefreshTokenService</c>. Rather than implementing
/// this interface from scratch, it is recommended to derive from the default implementation
/// and override its virtual methods — in particular <c>AcceptConsumedTokenAsync</c> — to
/// customize how consumed one-time-use tokens are handled (e.g. to add a grace period for
/// network failures or to treat replay as an attack and revoke access).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Validates the provided refresh token string against the given client, checking
    /// expiry, client binding, and whether the token has been consumed (for one-time-use tokens).
    /// </summary>
    /// <param name="token">The raw refresh token handle to validate.</param>
    /// <param name="client">The client that is presenting the refresh token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="TokenValidationResult"/> indicating whether the token is valid.
    /// On failure, the result contains an error code and description explaining the reason.
    /// </returns>
    Task<TokenValidationResult> ValidateRefreshTokenAsync(string token, Client client, Ct ct);

    /// <summary>
    /// Creates a new refresh token for the given request and persists it to the grant store.
    /// </summary>
    /// <param name="request">
    /// The refresh token creation request containing the subject, access token, and client
    /// for which the refresh token is being created.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The opaque refresh token handle (a string key) that the client should store and present
    /// when requesting new access tokens.
    /// </returns>
    Task<string> CreateRefreshTokenAsync(RefreshTokenCreationRequest request, Ct ct);

    /// <summary>
    /// Updates an existing refresh token according to the client's token usage policy
    /// (e.g. sliding expiration or one-time-use rotation) and persists the changes.
    /// </summary>
    /// <param name="request">
    /// The refresh token update request containing the current token handle, the refresh token model,
    /// and the client whose policy governs the update behavior.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The refresh token handle to return to the client. For sliding or absolute expiration tokens
    /// this may be the same handle; for one-time-use tokens a new handle is issued.
    /// </returns>
    Task<string> UpdateRefreshTokenAsync(RefreshTokenUpdateRequest request, Ct ct);
}
