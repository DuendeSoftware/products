// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for userinfo requests
/// </summary>
public interface IUserInfoRequestValidator
{
    /// <summary>
    /// Validates a userinfo request.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<UserInfoRequestValidationResult> ValidateRequestAsync(string accessToken, Ct ct);
}
