// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityModel;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Represents the results of validating a pushed authorization request.
/// </summary>
public class PushedAuthorizationValidationResult : ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="PushedAuthorizationValidationResult"/> class, indicating that
    /// PAR specific validation succeeded.
    /// </summary>
    /// <param name="validatedParRequest">The validated pushed authorization
    /// request.</param>
    public PushedAuthorizationValidationResult(
        ValidatedPushedAuthorizationRequest validatedParRequest)
    {
        IsError = false;
        ValidatedRequest = validatedParRequest;
    }

    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="PushedAuthorizationValidationResult"/> class, indicating that
    /// validation failed while validating the request specifically as for
    /// pushed authorization.
    /// </summary>
    /// <param name="error">The error code, as specified by RFC 9126,
    /// etc</param>
    /// <param name="errorDescription">The error description: "human-readable
    /// ASCII text providing additional information, used to assist the client
    /// developer in understanding the error that occurred."</param>
    public PushedAuthorizationValidationResult(
        string error,
        string errorDescription)
    {
        IsError = true;
        Error = error;
        ErrorDescription = errorDescription;
    }

    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="PushedAuthorizationValidationResult"/> class, indicating that
    /// validation failed while validating the pushed parameters for use as
    /// authorize parameters. In other words, the pushed parameters contain an
    /// error that would be an error even if the pushed parameters were used
    /// directly at the authorize endpoint.
    /// </summary>
    /// <param name="error">The error code, as specified by RFC 9126,
    /// etc</param>
    /// <param name="errorDescription">The error description: "human-readable
    /// ASCII text providing additional information, used to assist the client
    /// developer in understanding the error that occurred."</param>
    /// <param name="authorizeRequest">The partial results of validating the
    /// pushed authorize parameters.</param>
    public PushedAuthorizationValidationResult(
        string? error,
        string? errorDescription,
        ValidatedAuthorizeRequest authorizeRequest)
    {
        IsError = true;
        Error = error;
        ErrorDescription = errorDescription;
        PartiallyValidatedAuthorizeRequest = authorizeRequest;
    }

    public static PushedAuthorizationValidationResult CreateServerNonceResult(string nonce) =>
        new(OidcConstants.TokenErrors.UseDPoPNonce, null)
        {
            ServerIssuedNonce = nonce
        };


    /// <summary>
    /// The validated pushed authorization request, or null if a validation error occurred. 
    /// </summary>
    public ValidatedPushedAuthorizationRequest? ValidatedRequest { get; set; }

    /// <summary>
    /// The partially validated authorize request returned by the <see
    /// cref="IAuthorizeRequestValidator" /> when authorize request validation
    /// errors occur, or null otherwise.
    ///
    /// <para>If errors occur while the pushed authorization parameters are
    /// being validated as an authorize request, the <see
    /// cref="ValidatedAuthorizeRequest" /> that is being populated by that
    /// validation process can be used to enhance diagnostics and logging. 
    /// </para>
    /// </summary>
    public ValidatedAuthorizeRequest? PartiallyValidatedAuthorizeRequest { get; set; }

    public string? ServerIssuedNonce { get; set; }
}
