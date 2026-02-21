// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Contract for a service that performs high-level operations on pushed
/// authorization requests. 
/// </summary>
public interface IPushedAuthorizationService
{
    /// <summary>
    /// Serializes, data protects, and stores the pushed authorization request. 
    /// </summary>
    /// <param name="pushedAuthorizationRequest">The pushed authorization
    /// request without serialization or data protection applied</param>
    /// <param name="ct">The cancellation token.</param>
    ///
    Task StoreAsync(DeserializedPushedAuthorizationRequest pushedAuthorizationRequest, CT ct);

    /// <summary>
    /// Consumes the pushed authorization request, indicating that it should not
    /// be used again. Repeated use could indicate some form of replay attack,
    /// but also could indicate that an end user refreshed their browser or
    /// otherwise retried a request that consumed the pushed authorization
    /// request.
    /// </summary>
    /// <param name="referenceValue">The reference value of the pushed
    /// authorization request. The reference value is the identifier within the
    /// request_uri parameter.</param>
    /// <param name="ct">The cancellation token.</param>
    Task ConsumeAsync(string referenceValue, CT ct);

    /// <summary>
    /// Gets the raw pushed authorization parameters.
    /// </summary>
    /// <param name="referenceValue">The reference value of the pushed
    /// authorization request. The reference value is the identifier within the
    /// request_uri parameter.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized pushed authorization request, or null if the
    /// request does not exist or was previously consumed.
    /// </returns>
    Task<DeserializedPushedAuthorizationRequest?> GetPushedAuthorizationRequestAsync(string referenceValue, CT ct);
}
