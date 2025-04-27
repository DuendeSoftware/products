// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Interface for the userinfo response generator
/// </summary>
public interface ITokenRevocationResponseGenerator
{
    /// <summary>
    /// Creates the revocation endpoint response and processes the revocation request.
    /// </summary>
    /// <param name="validationResult">The userinfo request validation result.</param>
    Task<TokenRevocationResponse> ProcessAsync(TokenRevocationRequestValidationResult validationResult);
}
