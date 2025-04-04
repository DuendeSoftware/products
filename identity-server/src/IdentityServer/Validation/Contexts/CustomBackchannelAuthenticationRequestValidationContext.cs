// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Validation;

/// <summary>
/// The validation context for a custom CIBA validator.
/// </summary>
public class CustomBackchannelAuthenticationRequestValidationContext
{
    /// <summary>
    /// Creates a new instance of the <see cref="CustomBackchannelAuthenticationRequestValidationContext"/> 
    /// </summary>
    public CustomBackchannelAuthenticationRequestValidationContext(BackchannelAuthenticationRequestValidationResult validatedRequest) => ValidationResult = validatedRequest;
    /// <summary>
    /// Gets or sets the CIBA validation result.
    /// </summary>
    public BackchannelAuthenticationRequestValidationResult ValidationResult { get; set; }
}
