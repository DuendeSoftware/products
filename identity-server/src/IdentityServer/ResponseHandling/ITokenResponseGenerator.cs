// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Validation;
using System.Threading.Tasks;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Interface the token response generator
/// </summary>
public interface ITokenResponseGenerator
{
    /// <summary>
    /// Processes the response.
    /// </summary>
    /// <param name="validationResult">The validation result.</param>
    /// <returns></returns>
    Task<TokenResponse> ProcessAsync(TokenRequestValidationResult validationResult);
}
