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
    /// <param name="parameters"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="subject"></param>
    /// <param name="authorizeRequestType"></param>
    /// <returns></returns>
    Task<AuthorizeRequestValidationResult> ValidateAsync(NameValueCollection parameters, CT ct, ClaimsPrincipal? subject = null, AuthorizeRequestType authorizeRequestType = AuthorizeRequestType.Authorize);
}
