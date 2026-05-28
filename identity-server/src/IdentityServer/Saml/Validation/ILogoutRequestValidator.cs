// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// Validator for LogoutRequest
/// </summary>
public interface ILogoutRequestValidator
{
    /// <summary>
    /// Validate a LogoutRequest
    /// </summary>
    /// <param name="request">LogoutRequest validation context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Validation result</returns>
    Task<LogoutRequestValidationResult> ValidateAsync(ValidatedLogoutRequest request, Ct ct);
}
