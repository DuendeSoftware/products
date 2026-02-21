// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// The interface for a service that stores pushed authorization requests.
/// </summary>
public interface IPushedAuthorizationRequestStore
{
    /// <summary>
    /// Stores the pushed authorization request.
    /// </summary>
    /// <param name="pushedAuthorizationRequest">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task StoreAsync(PushedAuthorizationRequest pushedAuthorizationRequest, CT ct);

    /// <summary>
    /// Consumes the pushed authorization request, indicating that it should not
    /// be used again. Repeated use could indicate some form of replay attack,
    /// but also could indicate that an end user refreshed their browser or
    /// otherwise retried a request that consumed the pushed authorization
    /// request.
    /// </summary>
    /// <param name="referenceValueHash">The hash of the reference value of the
    /// pushed authorization request. The reference value is the identifier
    /// within the request_uri parameter.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task ConsumeByHashAsync(string referenceValueHash, CT ct);

    /// <summary>
    /// Gets the pushed authorization request.
    /// </summary>
    /// <param name="referenceValueHash">The hash of the reference value of the
    /// pushed authorization request. The reference value is the identifier
    /// within the request_uri parameter.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The pushed authorization request, or null if the request does
    /// not exist or was previously consumed.
    /// </returns>
    Task<PushedAuthorizationRequest?> GetByHashAsync(string referenceValueHash, CT ct);
}
