// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Specialized;
using System.Security.Claims;

namespace Duende.IdentityServer.Validation;

/// <summary>
///  Authorize endpoint request validator.
/// </summary>
public interface IAuthorizeRequestValidator
{
    /// <summary>
    ///  Validates authorize request parameters.
    /// </summary>
    /// <param name="parameters">The raw name/value collection of query string or form-post parameters from the authorize request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="subject">The authenticated user's claims principal, or <c>null</c> if the user has not yet authenticated.</param>
    /// <param name="authorizeRequestType">The type of authorize request being validated (e.g., initial authorize vs. callback after login).</param>
    /// <returns>An <see cref="AuthorizeRequestValidationResult"/> representing the outcome of validation.</returns>
    Task<AuthorizeRequestValidationResult> ValidateAsync(NameValueCollection parameters, Ct ct, ClaimsPrincipal? subject = null, AuthorizeRequestType authorizeRequestType = AuthorizeRequestType.Authorize);
}
