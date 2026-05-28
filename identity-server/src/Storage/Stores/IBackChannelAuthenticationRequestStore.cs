// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves backchannel authentication requests used in the Client-Initiated
/// Backchannel Authentication (CIBA) flow. In CIBA, a client initiates authentication
/// out-of-band and the user approves the request on a separate authentication device.
/// This store manages the lifecycle of those pending requests, keyed by both an
/// authentication request ID (exposed to the client) and an internal identifier.
/// </summary>
public interface IBackChannelAuthenticationRequestStore
{
    /// <summary>
    /// Persists a new backchannel authentication request and returns its unique
    /// authentication request ID. The returned ID is sent to the client as the
    /// <c>auth_req_id</c> parameter and is used to poll the token endpoint.
    /// </summary>
    /// <param name="request">The backchannel authentication request to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The authentication request ID (<c>auth_req_id</c>) assigned to the stored
    /// request. This value is returned to the client to poll for completion.
    /// </returns>
    Task<string> CreateRequestAsync(BackChannelAuthenticationRequest request, Ct ct);

    /// <summary>
    /// Gets all pending backchannel authentication requests for the specified user,
    /// optionally filtered to a specific client.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user whose requests to retrieve.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="clientId">
    /// An optional client identifier to further filter the results to requests from a
    /// specific client. When <see langword="null"/>, requests from all clients are returned.
    /// </param>
    /// <returns>
    /// A read-only collection of <see cref="BackChannelAuthenticationRequest"/> objects
    /// for the specified user (and optionally client). Returns an empty collection when
    /// no matching requests exist.
    /// </returns>
    Task<IReadOnlyCollection<BackChannelAuthenticationRequest>> GetLoginsForUserAsync(string subjectId, Ct ct, string? clientId = null);

    /// <summary>
    /// Gets a backchannel authentication request by its authentication request ID
    /// (<c>auth_req_id</c>). This is the externally visible identifier used by the
    /// client when polling the token endpoint.
    /// </summary>
    /// <param name="requestId">The authentication request ID to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="BackChannelAuthenticationRequest"/> with the specified
    /// <paramref name="requestId"/>, or <see langword="null"/> if no matching request exists.
    /// </returns>
    Task<BackChannelAuthenticationRequest?> GetByAuthenticationRequestIdAsync(string requestId, Ct ct);

    /// <summary>
    /// Gets a backchannel authentication request by its internal store identifier.
    /// The internal ID is used within IdentityServer to reference the request without
    /// exposing the externally visible authentication request ID.
    /// </summary>
    /// <param name="id">The internal identifier of the request to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="BackChannelAuthenticationRequest"/> with the specified internal
    /// <paramref name="id"/>, or <see langword="null"/> if no matching request exists.
    /// </returns>
    Task<BackChannelAuthenticationRequest?> GetByInternalIdAsync(string id, Ct ct);

    /// <summary>
    /// Removes the backchannel authentication request identified by its internal store
    /// identifier. This is called after the request has been completed or has expired.
    /// </summary>
    /// <param name="id">The internal identifier of the request to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveByInternalIdAsync(string id, Ct ct);

    /// <summary>
    /// Updates an existing backchannel authentication request identified by its internal
    /// store identifier. This is called when the user approves or denies the request on
    /// the authentication device.
    /// </summary>
    /// <param name="id">The internal identifier of the request to update.</param>
    /// <param name="request">The updated request data to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateByInternalIdAsync(string id, BackChannelAuthenticationRequest request, Ct ct);
}
