// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validation service for pushed authorization requests. Note that, in addition
/// to validation performed specially for pushed authorization requests, the
/// pushed parameters should be validated in the same way as an authorization
/// request sent to the authorization endpoint. Typical implementations of this
/// service will delegate to the <see cref="IAuthorizeRequestValidator"/> for
/// this purpose.
/// </summary>
public interface IPushedAuthorizationRequestValidator
{
    /// <summary>
    /// Validates the pushed authorization request.
    /// </summary>
    /// <param name="context">The validation context</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A  task containing a pushed authorization result that either
    /// wraps the validated request values or indicates the error code and
    /// description.</returns>
    Task<PushedAuthorizationValidationResult> ValidateAsync(PushedAuthorizationRequestValidationContext context, CT ct);
}
