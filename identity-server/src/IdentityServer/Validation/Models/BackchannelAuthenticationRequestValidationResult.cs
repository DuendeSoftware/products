// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validation result for backchannel authentication requests
/// </summary>
public class BackchannelAuthenticationRequestValidationResult : ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackchannelAuthenticationRequestValidationResult"/> class.
    /// </summary>
    /// <param name="validatedRequest">The validated request.</param>
    public BackchannelAuthenticationRequestValidationResult(ValidatedBackchannelAuthenticationRequest validatedRequest)
    {
        IsError = false;

        ValidatedRequest = validatedRequest;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackchannelAuthenticationRequestValidationResult"/> class.
    /// </summary>
    /// <param name="validatedRequest">The validated request.</param>
    /// <param name="error">The error.</param>
    /// <param name="errorDescription">The error description.</param>
    public BackchannelAuthenticationRequestValidationResult(ValidatedBackchannelAuthenticationRequest validatedRequest, string error, string errorDescription = null)
    {
        IsError = true;

        Error = error;
        ErrorDescription = errorDescription;
        ValidatedRequest = validatedRequest;
    }

    /// <summary>
    /// Gets the validated request.
    /// </summary>
    /// <value>
    /// The validated request.
    /// </value>
    public ValidatedBackchannelAuthenticationRequest ValidatedRequest { get; }
}