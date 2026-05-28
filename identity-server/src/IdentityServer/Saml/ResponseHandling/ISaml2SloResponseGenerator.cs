// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Validation;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Response generator for SAML 2.0 Single Logout
/// </summary>
public interface ISaml2SloResponseGenerator
{
    /// <summary>
    /// Create a success LogoutResponse for a validated LogoutRequest.
    /// </summary>
    /// <param name="request">The validated logout request context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Saml2 front channel result containing the LogoutResponse</returns>
    Task<Saml2FrontChannelResult> CreateSuccessResponse(ValidatedLogoutRequest request, Ct ct);

    /// <summary>
    /// Create a partial logout LogoutResponse indicating not all SPs successfully logged out.
    /// </summary>
    /// <param name="request">The validated logout request context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Saml2 front channel result containing the LogoutResponse with partial logout status</returns>
    Task<Saml2FrontChannelResult> CreatePartialLogoutResponse(ValidatedLogoutRequest request, Ct ct);
}
