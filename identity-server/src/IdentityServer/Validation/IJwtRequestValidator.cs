// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Interface for request object validator
/// </summary>
public interface IJwtRequestValidator
{
    /// <summary>
    /// Validates a JWT request object
    /// </summary>
    /// <param name="context">The validation context.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<JwtRequestValidationResult> ValidateAsync(JwtRequestValidationContext context, Ct ct);
}
