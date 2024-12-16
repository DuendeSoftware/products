// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for handling API client authentication.
/// </summary>
public interface IApiSecretValidator
{
    /// <summary>
    /// Tries to authenticate an API client based on the incoming request
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    Task<ApiSecretValidationResult> ValidateAsync(HttpContext context);
}
