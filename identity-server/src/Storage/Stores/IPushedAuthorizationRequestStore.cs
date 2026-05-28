// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists, retrieves, and consumes pushed authorization requests (PAR) as defined by
/// RFC 9126. In the PAR flow, a client first pushes its authorization parameters to the
/// PAR endpoint and receives a <c>request_uri</c>. The client then uses that URI at the
/// authorization endpoint instead of sending parameters in the query string. This store
/// manages the lifecycle of those pending requests, keyed by a hash of the reference
/// value embedded in the <c>request_uri</c>.
/// </summary>
public interface IPushedAuthorizationRequestStore
{
    /// <summary>
    /// Persists a new pushed authorization request.
    /// </summary>
    /// <param name="pushedAuthorizationRequest">The pushed authorization request to store.</param>
    /// <param name="ct">The cancellation token.</param>
    Task StoreAsync(PushedAuthorizationRequest pushedAuthorizationRequest, Ct ct);

    /// <summary>
    /// Marks a pushed authorization request as consumed so that it cannot be used again.
    /// Repeated use of the same <c>request_uri</c> could indicate a replay attack, but
    /// may also occur when an end user refreshes their browser during the authorization
    /// flow.
    /// </summary>
    /// <param name="referenceValueHash">
    /// The hash of the reference value embedded in the <c>request_uri</c> parameter
    /// (i.e., the portion after <c>urn:ietf:params:oauth:request_uri:</c>).
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task ConsumeByHashAsync(string referenceValueHash, Ct ct);

    /// <summary>
    /// Gets the pushed authorization request identified by the hash of its reference value.
    /// </summary>
    /// <param name="referenceValueHash">
    /// The hash of the reference value embedded in the <c>request_uri</c> parameter
    /// (i.e., the portion after <c>urn:ietf:params:oauth:request_uri:</c>).
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="PushedAuthorizationRequest"/> associated with the specified hash,
    /// or <see langword="null"/> if the request does not exist or was previously consumed.
    /// </returns>
    Task<PushedAuthorizationRequest?> GetByHashAsync(string referenceValueHash, Ct ct);
}
