// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Extensibility point for CIBA authentication request validation.
/// </summary>
public interface ICustomBackchannelAuthenticationValidator
{
    /// <summary>
    /// Validates a CIBA authentication request.
    /// </summary>
    /// <param name="customValidationContext"></param>
    Task ValidateAsync(CustomBackchannelAuthenticationRequestValidationContext customValidationContext);
}
