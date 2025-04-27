// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


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
    /// <param name="subject"></param>
    /// <param name="authorizeRequestType"></param>
    Task<AuthorizeRequestValidationResult> ValidateAsync(NameValueCollection parameters, ClaimsPrincipal subject = null, AuthorizeRequestType authorizeRequestType = AuthorizeRequestType.Authorize);
}
