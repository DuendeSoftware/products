// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Retrieves access tokens
/// </summary>
public interface IAccessTokenRetriever
{
    /// <summary>
    /// Asynchronously gets the access token.
    /// </summary>
    /// <param name="context">Context used to retrieve the token.</param>
    /// <returns>A task that contains the access token result, which is an
    /// object model that can represent various types of tokens (bearer, dpop),
    /// the absence of an optional token, or an error. </returns>
    Task<AccessTokenResult> GetAccessToken(AccessTokenRetrievalContext context);
}
