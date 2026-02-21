// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Services;

/// <summary>
/// The claims service is responsible for determining which claims to include in tokens
/// </summary>
public interface IClaimsService
{
    /// <summary>
    /// Returns claims for an identity token
    /// </summary>
    /// <param name="subject">The subject</param>
    /// <param name="resources">The resources.</param>
    /// <param name="includeAllIdentityClaims">Specifies if all claims should be included in the token, or if the userinfo endpoint can be used to retrieve them</param>
    /// <param name="request">The raw request</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// Claims for the identity token
    /// </returns>
    Task<IEnumerable<Claim>> GetIdentityTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resources, bool includeAllIdentityClaims, ValidatedRequest request, CT ct);

    /// <summary>
    /// Returns claims for an access token.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="resources">The resources.</param>
    /// <param name="request">The raw request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// Claims for the access token
    /// </returns>
    Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resources, ValidatedRequest request, CT ct);
}
