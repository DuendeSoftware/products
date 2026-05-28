// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Allows inserting custom validation logic into the access token and identity token validation pipelines.
/// </summary>
/// <remarks>
/// Implement this interface to run additional checks after IdentityServer has completed its
/// built-in token validation (signature, expiry, issuer, audience, etc.). Both
/// <see cref="ValidateAccessTokenAsync"/> and <see cref="ValidateIdentityTokenAsync"/> receive
/// the result of the preceding built-in validation and may inspect, enrich, or override it.
/// <para>
/// These methods are invoked at the introspection endpoint, the userinfo endpoint, and anywhere
/// else IdentityServer validates tokens internally (e.g., during token exchange or logout).
/// </para>
/// <para>
/// To fail validation, set <c>IsError</c> and <c>Error</c> on the returned
/// <see cref="TokenValidationResult"/>. To add claims or modify the result, update the
/// <c>Claims</c> collection or other properties before returning.
/// </para>
/// <para>
/// Register implementations using <c>AddCustomTokenValidator&lt;T&gt;()</c> on the
/// IdentityServer builder. Multiple implementations may be registered and are all invoked in order.
/// </para>
/// </remarks>
public interface ICustomTokenValidator
{
    /// <summary>
    /// Executes custom validation logic for an access token after built-in validation has completed.
    /// </summary>
    /// <param name="result">
    /// The <see cref="TokenValidationResult"/> produced by the preceding built-in validation steps.
    /// Inspect this to determine whether validation already failed, and return a modified or
    /// replacement result as appropriate.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="TokenValidationResult"/> representing the final validation outcome.
    /// Set <c>IsError</c> to <c>true</c> to reject the token.
    /// </returns>
    Task<TokenValidationResult> ValidateAccessTokenAsync(TokenValidationResult result, Ct ct);

    /// <summary>
    /// Executes custom validation logic for an identity token after built-in validation has completed.
    /// </summary>
    /// <param name="result">
    /// The <see cref="TokenValidationResult"/> produced by the preceding built-in validation steps.
    /// Inspect this to determine whether validation already failed, and return a modified or
    /// replacement result as appropriate.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="TokenValidationResult"/> representing the final validation outcome.
    /// Set <c>IsError</c> to <c>true</c> to reject the token.
    /// </returns>
    Task<TokenValidationResult> ValidateIdentityTokenAsync(TokenValidationResult result, Ct ct);
}
