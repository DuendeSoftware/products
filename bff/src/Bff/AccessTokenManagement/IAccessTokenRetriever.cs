// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.AccessTokenManagement;

/// <summary>
/// Retrieves access tokens
/// </summary>
public interface IAccessTokenRetriever
{
    /// <summary>
    /// Asynchronously gets the access token.
    /// </summary>
    /// <returns>A task that contains the access token result, which is an
    /// object model that can represent various types of tokens (bearer, dpop),
    /// the absence of an optional token, or an error. </returns>
    public Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, CT ct = default);
}
