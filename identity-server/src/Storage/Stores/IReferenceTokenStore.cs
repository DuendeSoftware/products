// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves reference tokens. When a client is configured to use reference
/// tokens instead of self-contained JWTs, IdentityServer stores the token data server-side
/// and issues an opaque handle to the client. Resource servers then introspect the handle
/// to retrieve the token data. This store manages the lifecycle of those server-side token
/// records, which are backed by <see cref="IPersistedGrantStore"/> by default.
/// </summary>
public interface IReferenceTokenStore
{
    /// <summary>
    /// Persists a new reference token and returns the opaque handle that identifies it.
    /// The handle is issued to the client and used later to look up the token during
    /// introspection.
    /// </summary>
    /// <param name="token">The token data to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The opaque handle string that the client uses to reference this token (e.g., when
    /// presenting it to a resource server for introspection).
    /// </returns>
    Task<string> StoreReferenceTokenAsync(Token token, Ct ct);

    /// <summary>
    /// Retrieves the reference token associated with the specified handle.
    /// </summary>
    /// <param name="handle">The opaque handle that identifies the reference token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="Token"/> associated with the specified <paramref name="handle"/>,
    /// or <see langword="null"/> if no matching token exists or the token has expired.
    /// </returns>
    Task<Token?> GetReferenceTokenAsync(string handle, Ct ct);

    /// <summary>
    /// Removes the reference token identified by the specified handle.
    /// </summary>
    /// <param name="handle">The opaque handle of the reference token to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveReferenceTokenAsync(string handle, Ct ct);

    /// <summary>
    /// Removes all reference tokens issued to the specified subject and client,
    /// optionally scoped to a particular session.
    /// </summary>
    /// <param name="subjectId">The subject identifier whose tokens should be removed.</param>
    /// <param name="clientId">The client identifier whose tokens should be removed.</param>
    /// <param name="sessionId">
    /// An optional session identifier to further restrict removal to tokens from a
    /// specific session. When <see langword="null"/>, tokens from all sessions are removed.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveReferenceTokensAsync(string subjectId, string clientId, string? sessionId, Ct ct);
}
