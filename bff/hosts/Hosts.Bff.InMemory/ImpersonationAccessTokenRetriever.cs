// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;

namespace Bff;

public class ImpersonationAccessTokenRetriever : DefaultAccessTokenRetriever
{
    public override async Task<AccessTokenResult> GetAccessToken(AccessTokenRetrievalContext context)
    {
        var result = await base.GetAccessToken(context);

        if (result is BearerTokenResult bearerToken)
        {
            var client = new HttpClient();
            var exchangeResponse = await client.RequestTokenExchangeTokenAsync(new TokenExchangeTokenRequest
            {
                Address = "https://localhost:5001/connect/token",
                GrantType = OidcConstants.GrantTypes.TokenExchange,

                ClientId = "bff",
                ClientSecret = "secret",

                SubjectToken = bearerToken.AccessToken,
                SubjectTokenType = OidcConstants.TokenTypeIdentifiers.AccessToken
            });
            if (exchangeResponse.AccessToken is null)
            {
                return new NoAccessTokenReturnedError("Token exchanged failed. Access token is null");
            }
            if (exchangeResponse.IsError)
            {
                return new AccessTokenRetrievalError($"Token exchanged failed: {exchangeResponse.ErrorDescription}");
            }
            return new BearerTokenResult(exchangeResponse.AccessToken);
        }

        return result;
    }
}
