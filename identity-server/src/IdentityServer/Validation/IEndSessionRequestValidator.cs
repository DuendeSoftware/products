// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Specialized;
using System.Security.Claims;

namespace Duende.IdentityServer.Validation;

/// <summary>
///  Validates end session requests.
/// </summary>
public interface IEndSessionRequestValidator
{
    /// <summary>
    /// Validates end session endpoint requests.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="subject"></param>
    Task<EndSessionValidationResult> ValidateAsync(NameValueCollection parameters, ClaimsPrincipal subject);

    /// <summary>
    ///  Validates requests from logout page iframe to trigger single signout.
    /// </summary>
    /// <param name="parameters"></param>
    Task<EndSessionCallbackValidationResult> ValidateCallbackAsync(NameValueCollection parameters);
}
