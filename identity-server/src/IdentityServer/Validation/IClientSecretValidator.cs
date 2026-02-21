// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for handling client authentication
/// </summary>
public interface IClientSecretValidator
{
    /// <summary>
    /// Tries to authenticate a client based on the incoming request
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<ClientSecretValidationResult> ValidateAsync(HttpContext context, CT ct);
}
