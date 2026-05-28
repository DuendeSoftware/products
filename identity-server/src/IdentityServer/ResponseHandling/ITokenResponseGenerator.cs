// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the response returned from the token endpoint for a successfully validated token
/// request. The response is an object model describing the tokens and metadata that will be
/// serialized into the HTTP response body. This interface is invoked after the token request has
/// been validated and covers all supported grant types: authorization code, client credentials,
/// resource owner password, refresh token, device code, CIBA, and extension grants.
/// </summary>
/// <remarks>
/// The default implementation is <c>TokenResponseGenerator</c>, which contains virtual methods
/// for each grant type (e.g., <c>ProcessAuthorizationCodeRequestAsync</c>,
/// <c>ProcessClientCredentialsRequestAsync</c>, <c>ProcessRefreshTokenRequestAsync</c>). To
/// customize token response behavior, derive from <c>TokenResponseGenerator</c> and override the
/// appropriate virtual method for the grant type you want to modify, rather than implementing
/// this interface from scratch.
/// </remarks>
public interface ITokenResponseGenerator
{
    /// <summary>
    /// Processes a validated token request and produces the token endpoint response.
    /// </summary>
    /// <param name="validationResult">
    /// The result of validating the token request, including the grant type, client, requested
    /// scopes, and any associated authorization artifacts such as authorization codes or refresh
    /// tokens.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="TokenResponse"/> containing the issued tokens (access token, identity token,
    /// refresh token), their lifetimes, the granted scope, and any custom properties.
    /// </returns>
    Task<TokenResponse> ProcessAsync(TokenRequestValidationResult validationResult, Ct ct);
}
