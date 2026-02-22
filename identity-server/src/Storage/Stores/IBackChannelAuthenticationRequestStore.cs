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
    Task<string> CreateRequestAsync(BackChannelAuthenticationRequest request, Ct ct);

    /// <summary>
    /// Gets the requests.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="clientId">The client identifier.</param>
    Task<IEnumerable<BackChannelAuthenticationRequest>> GetLoginsForUserAsync(string subjectId, Ct ct, string? clientId = null);

    /// <summary>
    /// Gets the request.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<BackChannelAuthenticationRequest?> GetByAuthenticationRequestIdAsync(string requestId, Ct ct);

    /// <summary>
    /// Gets the request.
    /// </summary>
    /// <param name="id">The internal identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<BackChannelAuthenticationRequest?> GetByInternalIdAsync(string id, Ct ct);

    /// <summary>
    /// Removes the request.
    /// </summary>
    /// <param name="id">The internal identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveByInternalIdAsync(string id, Ct ct);

    /// <summary>
    /// Updates the request.
    /// </summary>
    /// <param name="id">The internal identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateByInternalIdAsync(string id, BackChannelAuthenticationRequest request, Ct ct);
}
