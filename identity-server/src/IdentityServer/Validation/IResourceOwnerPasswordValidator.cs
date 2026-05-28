// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates resource owner password credentials submitted to the token endpoint.
/// </summary>
/// <remarks>
/// Implement this interface to support the OAuth 2.0 Resource Owner Password Credentials (ROPC)
/// grant type (<c>grant_type=password</c>). IdentityServer invokes <see cref="ValidateAsync"/>
/// when a client submits a username and password directly to the token endpoint.
/// <para>
/// The implementation is responsible for authenticating the supplied credentials against the
/// user store (e.g., ASP.NET Core Identity or a custom store) and populating
/// <c>ResourceOwnerPasswordValidationContext.Result</c> with a <c>GrantValidationResult</c>
/// that identifies the authenticated subject.
/// </para>
/// <para>
/// To fail the request, create a <c>GrantValidationResult</c> with an appropriate
/// <c>TokenRequestErrors</c> value (e.g., <c>InvalidGrant</c>) and assign it to
/// <c>context.Result</c>.
/// </para>
/// <para>
/// Register the implementation using <c>AddResourceOwnerValidator&lt;T&gt;()</c> on the
/// IdentityServer builder.
/// </para>
/// <para>
/// Note: The ROPC grant is considered legacy and is
/// <see href="https://docs.duendesoftware.com/identityserver/tokens/extension-grants/ropc">not recommended for new applications</see>.
/// Consider using more secure flows such as the authorization code flow with PKCE.
/// </para>
/// </remarks>
public interface IResourceOwnerPasswordValidator
{
    /// <summary>
    /// Validates the resource owner password credentials supplied in the token request.
    /// </summary>
    /// <param name="context">
    /// The validation context, providing access to the username, password, and the validated
    /// token request. Set <c>context.Result</c> to a <c>GrantValidationResult</c> to indicate
    /// success or failure.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when validation is finished.</returns>
    Task ValidateAsync(ResourceOwnerPasswordValidationContext context, Ct ct);
}
