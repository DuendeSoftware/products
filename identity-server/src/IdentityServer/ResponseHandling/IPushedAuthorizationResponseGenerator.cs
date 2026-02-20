// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;


/// <summary>
/// Service that generates response models for the pushed authorization endpoint. This service encapsulates the behavior that
/// is needed to create a response model from a validated request. 
/// </summary>
public interface IPushedAuthorizationResponseGenerator
{
    /// <summary>
    /// Asynchronously creates a response model from a validated pushed authorization request.
    /// </summary>
    /// <param name="request">The validated pushed authorization request.</param>
    /// <param name="ct">The <see cref="CT"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A task that contains response model indicating either success or failure.</returns>
    Task<PushedAuthorizationResponse> CreateResponseAsync(ValidatedPushedAuthorizationRequest request, CT ct);
}
