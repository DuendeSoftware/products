// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// Default implementation of IAccessTokenRetriever
/// </summary>
public class DefaultAccessTokenRetriever() : IAccessTokenRetriever
{
    /// <inheritdoc />
    public virtual async Task<AccessTokenResult> GetAccessToken(AccessTokenRetrievalContext context)
    {
        if (context.Metadata.RequiredTokenType.HasValue)
        {
            return await context.HttpContext.GetManagedAccessToken(
                tokenType: context.Metadata.RequiredTokenType.Value,
                optional: false,
                context.UserTokenRequestParameters);
        }
        else if (context.Metadata.OptionalUserToken)
        {
            return await context.HttpContext.GetManagedAccessToken(
                tokenType: TokenType.User,
                optional: true,
                context.UserTokenRequestParameters);
        }
        else
        {
            return new NoAccessTokenResult();
        }
    }
}
