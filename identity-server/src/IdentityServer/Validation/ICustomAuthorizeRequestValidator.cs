// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Allows inserting custom validation logic into authorization requests at the authorization endpoint.
/// </summary>
/// <remarks>
/// Implement this interface to run custom code as part of the authorization request pipeline.
/// <see cref="ValidateAsync"/> is called during authorize request processing, after all built-in
/// validation has succeeded. The context provides access to the validated request and the
/// response that will be sent to the client.
/// <para>
/// Within the method you can inspect request parameters and apply additional business rules
/// (e.g., restricting which clients or users may use certain scopes or ACR values).
/// </para>
/// <para>
/// To fail the request, set <c>IsError</c>, <c>Error</c>, and optionally <c>ErrorDescription</c>
/// on the <c>Result</c> object of the <see cref="CustomAuthorizeRequestValidationContext"/>.
/// </para>
/// <para>
/// Register implementations using <c>AddCustomAuthorizeRequestValidator&lt;T&gt;()</c> on the
/// IdentityServer builder. Multiple implementations may be registered and are all invoked.
/// </para>
/// </remarks>
public interface ICustomAuthorizeRequestValidator
{
    /// <summary>
    /// Executes custom validation logic for an authorization request.
    /// </summary>
    /// <param name="context">
    /// The validation context, providing access to the validated authorization request and the
    /// response that will be returned to the client. Use <c>context.Result</c> to signal failure.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when validation is finished.</returns>
    Task ValidateAsync(CustomAuthorizeRequestValidationContext context, Ct ct);
}
