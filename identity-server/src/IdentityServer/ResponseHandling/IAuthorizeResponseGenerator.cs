// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the authorization endpoint response after all interaction requirements have been
/// satisfied. Depending on the requested response type, this produces an authorization code,
/// tokens, or both, and packages them into an <see cref="AuthorizeResponse"/> that is then
/// serialized and returned to the client's redirect URI.
/// </summary>
/// <remarks>
/// This interface is invoked after <see cref="IAuthorizeInteractionResponseGenerator"/> has
/// confirmed that no further user interaction is required. The default implementation handles
/// the authorization code flow and can be replaced or extended to support additional response
/// types or to customize the content of the authorization response.
/// </remarks>
public interface IAuthorizeResponseGenerator
{
    /// <summary>
    /// Creates the authorization response for a validated authorize request.
    /// </summary>
    /// <param name="request">The validated authorize request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// An <see cref="AuthorizeResponse"/> containing the authorization code, tokens, and/or
    /// other parameters to be returned to the client via the redirect URI.
    /// </returns>
    Task<AuthorizeResponse> CreateResponseAsync(ValidatedAuthorizeRequest request, Ct ct);
}
