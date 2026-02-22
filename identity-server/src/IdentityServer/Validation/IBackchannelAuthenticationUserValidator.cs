// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Interface for the backchannel authentication user validation
/// </summary>
public interface IBackchannelAuthenticationUserValidator
{
    /// <summary>
    /// Validates the user.
    /// </summary>
    /// <param name="userValidatorContext"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<BackchannelAuthenticationUserValidationResult> ValidateRequestAsync(BackchannelAuthenticationUserValidatorContext userValidatorContext, Ct ct);
}
