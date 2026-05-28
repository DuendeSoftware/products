// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;


/// <summary>
/// Generates response models for the pushed authorization endpoint (RFC 9126). The response
/// contains a <c>request_uri</c> that the client can use in a subsequent authorization request
/// to reference the pushed authorization parameters, along with the expiration of that URI.
/// This service encapsulates the behavior needed to create a response model from a validated
/// pushed authorization request.
/// </summary>
/// <remarks>
/// The default implementation stores the pushed authorization request and returns the
/// <c>request_uri</c> and expiration. Override this interface or extend the default
/// implementation to customize the pushed authorization response, for example to change the
/// format of the <c>request_uri</c> or to adjust the expiration time.
/// </remarks>
public interface IPushedAuthorizationResponseGenerator
{
    /// <summary>
    /// Asynchronously creates a response model from a validated pushed authorization request.
    /// </summary>
    /// <param name="request">The validated pushed authorization request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A task that contains a <see cref="PushedAuthorizationResponse"/> indicating either
    /// success (with the <c>request_uri</c> and expiration) or failure.
    /// </returns>
    Task<PushedAuthorizationResponse> CreateResponseAsync(ValidatedPushedAuthorizationRequest request, Ct ct);
}
