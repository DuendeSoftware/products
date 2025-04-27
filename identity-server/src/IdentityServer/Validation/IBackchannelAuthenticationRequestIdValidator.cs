// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// The backchannel authentication request id validator
/// </summary>
public interface IBackchannelAuthenticationRequestIdValidator
{
    /// <summary>
    /// Validates the authentication request id.
    /// </summary>
    /// <param name="context">The context.</param>
    Task ValidateAsync(BackchannelAuthenticationRequestIdValidationContext context);
}
