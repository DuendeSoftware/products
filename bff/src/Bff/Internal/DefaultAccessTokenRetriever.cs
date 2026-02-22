// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Internal;

/// <summary>
/// Default implementation of IAccessTokenRetriever
/// </summary>
internal class DefaultAccessTokenRetriever() : IAccessTokenRetriever
{
    /// <inheritdoc />
    public async Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, Ct ct = default)
    {
        if (context.Metadata.TokenType.HasValue)
        {
            return await context.HttpContext.GetManagedAccessToken(
                requiredTokenType: context.Metadata.TokenType.Value,
                context.UserTokenRequestParameters, ct: ct);
        }
        else
        {
            return new NoAccessTokenResult();
        }
    }
}
