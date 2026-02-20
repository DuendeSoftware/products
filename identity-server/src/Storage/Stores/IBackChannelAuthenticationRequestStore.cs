// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for the backchannel authentication request store
/// </summary>
public interface IBackChannelAuthenticationRequestStore
{
    /// <summary>
    /// Creates the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<string> CreateRequestAsync(BackChannelAuthenticationRequest request, CT ct);

    /// <summary>
    /// Gets the requests.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<IEnumerable<BackChannelAuthenticationRequest>> GetLoginsForUserAsync(string subjectId, string? clientId = null, CT ct = default);

    /// <summary>
    /// Gets the request.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<BackChannelAuthenticationRequest?> GetByAuthenticationRequestIdAsync(string requestId, CT ct);

    /// <summary>
    /// Gets the request.
    /// </summary>
    /// <param name="id">The internal identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<BackChannelAuthenticationRequest?> GetByInternalIdAsync(string id, CT ct);

    /// <summary>
    /// Removes the request.
    /// </summary>
    /// <param name="id">The internal identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveByInternalIdAsync(string id, CT ct);

    /// <summary>
    /// Updates the request.
    /// </summary>
    /// <param name="id">The internal identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateByInternalIdAsync(string id, BackChannelAuthenticationRequest request, CT ct);
}
