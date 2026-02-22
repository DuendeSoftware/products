// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for reference token storage
/// </summary>
public interface IReferenceTokenStore
{
    /// <summary>
    /// Stores the reference token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string> StoreReferenceTokenAsync(Token token, Ct ct);

    /// <summary>
    /// Gets the reference token.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<Token?> GetReferenceTokenAsync(string handle, Ct ct);

    /// <summary>
    /// Removes the reference token.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveReferenceTokenAsync(string handle, Ct ct);

    /// <summary>
    /// Removes the reference tokens.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveReferenceTokensAsync(string subjectId, string clientId, string? sessionId, Ct ct);
}
