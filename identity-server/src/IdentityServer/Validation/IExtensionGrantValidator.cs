// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Handles validation of token requests that use a custom (extension) OAuth grant type.
/// </summary>
/// <remarks>
/// Implement this interface to support a custom OAuth 2.0 extension grant type at the token endpoint.
/// IdentityServer routes incoming token requests whose <c>grant_type</c> parameter matches
/// <see cref="GrantType"/> to the corresponding <see cref="IExtensionGrantValidator"/> implementation.
/// <para>
/// <see cref="ValidateAsync"/> is responsible for authenticating the request — for example by
/// validating a custom credential or exchanging an external token — and then populating
/// <c>ExtensionGrantValidationContext.Result</c> with a <c>GrantValidationResult</c> that
/// identifies the subject (user) on whose behalf the token should be issued.
/// </para>
/// <para>
/// To fail the request, create a <c>GrantValidationResult</c> with an appropriate
/// <c>TokenRequestErrors</c> value and assign it to <c>context.Result</c>.
/// </para>
/// <para>
/// Register implementations using <c>AddExtensionGrantValidator&lt;T&gt;()</c> on the
/// IdentityServer builder.
/// </para>
/// </remarks>
public interface IExtensionGrantValidator
{
    /// <summary>
    /// Validates a token request that uses the custom grant type identified by <see cref="GrantType"/>.
    /// </summary>
    /// <param name="context">
    /// The validation context, providing access to the raw and validated token request parameters.
    /// Set <c>context.Result</c> to a <c>GrantValidationResult</c> to indicate success or failure.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when validation is finished.</returns>
    Task ValidateAsync(ExtensionGrantValidationContext context, Ct ct);

    /// <summary>
    /// Gets the custom grant type name that this validator handles.
    /// </summary>
    /// <remarks>
    /// The value must match the <c>grant_type</c> parameter sent by the client in the token request.
    /// The client must also be configured with this grant type in its <c>AllowedGrantTypes</c> list.
    /// </remarks>
    /// <value>The grant type string, e.g. <c>"my_custom_grant"</c>.</value>
    string GrantType { get; }
}
