// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Extensibility point for adding custom validation logic to Client-Initiated Backchannel
/// Authentication (CIBA) requests.
/// </summary>
/// <remarks>
/// Implement this interface to run additional validation after IdentityServer has completed its
/// built-in CIBA request validation. <see cref="ValidateAsync"/> is called during backchannel
/// authentication request processing, giving the implementation an opportunity to inspect or
/// reject the request based on application-specific rules.
/// <para>
/// Common use cases include enforcing additional constraints on the binding message, validating
/// custom request parameters, or applying per-client policies that are not covered by the
/// standard CIBA validation.
/// </para>
/// <para>
/// To fail the request, set the error details on the <c>Result</c> object of the
/// <see cref="CustomBackchannelAuthenticationRequestValidationContext"/>.
/// </para>
/// <para>
/// Register implementations using <c>AddCustomBackchannelAuthenticationValidator&lt;T&gt;()</c>
/// on the IdentityServer builder.
/// </para>
/// </remarks>
public interface ICustomBackchannelAuthenticationValidator
{
    /// <summary>
    /// Executes custom validation logic for a CIBA backchannel authentication request.
    /// </summary>
    /// <param name="customValidationContext">
    /// The validation context, providing access to the validated CIBA request parameters and
    /// the result object used to signal failure.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when validation is finished.</returns>
    Task ValidateAsync(CustomBackchannelAuthenticationRequestValidationContext customValidationContext, Ct ct);
}
