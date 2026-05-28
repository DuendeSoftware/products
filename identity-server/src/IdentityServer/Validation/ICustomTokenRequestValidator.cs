// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Allows inserting custom validation logic into token requests at the token endpoint.
/// </summary>
/// <remarks>
/// Implement this interface to run custom code as part of the token issuance pipeline.
/// <see cref="ValidateAsync"/> is called during token request processing, after all built-in
/// validation has succeeded. The context provides access to the validated request and the
/// response that will be sent to the client.
/// <para>
/// Within the method you can inspect and modify request parameters such as the token lifetime,
/// token type, confirmation method, and client claims. Use the <c>CustomResponse</c> dictionary
/// on the context to emit additional fields in the token endpoint response.
/// </para>
/// <para>
/// To fail the request, set <c>IsError</c>, <c>Error</c>, and optionally <c>ErrorDescription</c>
/// on the <c>Result</c> object of the <see cref="CustomTokenRequestValidationContext"/>.
/// </para>
/// <para>
/// Register implementations using <c>AddCustomTokenRequestValidator&lt;T&gt;()</c> on the
/// IdentityServer builder. Multiple implementations may be registered and are all invoked.
/// </para>
/// </remarks>
public interface ICustomTokenRequestValidator
{
    /// <summary>
    /// Executes custom validation logic for a token request.
    /// </summary>
    /// <param name="context">
    /// The validation context, providing access to the validated token request and the
    /// response that will be returned to the client. Use <c>context.Result</c> to signal
    /// failure or to modify response fields.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when validation is finished.</returns>
    Task ValidateAsync(CustomTokenRequestValidationContext context, Ct ct);
}
