// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates Demonstrating Proof of Possession (DPoP) proof tokens submitted to IdentityServer.
/// </summary>
/// <remarks>
/// DPoP (RFC 9449) binds access tokens to a client's asymmetric key pair, preventing token
/// replay by a different party. IdentityServer invokes this validator at the token endpoint
/// when a client submits a <c>DPoP</c> header, and at the userinfo/introspection endpoints
/// when a DPoP-bound access token is presented.
/// <para>
/// A default implementation is provided. Override this interface only when custom DPoP proof
/// validation logic is required (e.g., stricter nonce policies or additional claim checks).
/// </para>
/// <para>
/// The validator receives a <see cref="DPoPProofValidationContext"/> describing the HTTP method,
/// URL, proof token string, and optionally the access token to bind against. It returns a
/// <see cref="DPoPProofValidationResult"/> containing the extracted JWK, thumbprint, and
/// confirmation value, or error details if validation failed.
/// </para>
/// <para>
/// Register a custom implementation using <c>AddDPoPProofValidator&lt;T&gt;()</c> on the
/// IdentityServer builder.
/// </para>
/// </remarks>
public interface IDPoPProofValidator
{
    /// <summary>
    /// Validates a DPoP proof token for the current request.
    /// </summary>
    /// <param name="context">
    /// The validation context, containing the proof token string, the HTTP method and URL to
    /// validate, expiration settings, and optionally the access token to bind against.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="DPoPProofValidationResult"/> that indicates success or failure. On success,
    /// the result contains the extracted JWK, thumbprint, confirmation value, and payload claims.
    /// On failure, <c>IsError</c> is <c>true</c> and <c>Error</c>/<c>ErrorDescription</c> are set.
    /// </returns>
    Task<DPoPProofValidationResult> ValidateAsync(DPoPProofValidationContext context, Ct ct);
}
