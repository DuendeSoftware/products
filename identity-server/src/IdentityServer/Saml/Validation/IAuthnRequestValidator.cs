// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// Validator for AuthnRequest
/// </summary>
public interface IAuthnRequestValidator
{
    /// <summary>
    /// Validate an AuthnRequest
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Validation result</returns>
    public Task<AuthnRequestValidationResult> ValidateAsync(ValidatedAuthnRequest request, Ct ct);
}
