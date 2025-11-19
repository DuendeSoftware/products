// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;

namespace Bff;

public class ImpersonationAccessTokenRetriever(IAccessTokenRetriever inner) : IAccessTokenRetriever
{
    public async Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, CancellationToken ct = default)
    {
        var result = await inner.GetAccessTokenAsync(context, ct);

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
            }, cancellationToken: ct);
            if (exchangeResponse.AccessToken is null)
            {
                return new AccessTokenRetrievalError
                {
                    Error = "Token exchanged failed. Access token is null"
                };
            }

            if (exchangeResponse.IsError)
            {
                return new AccessTokenRetrievalError
                {
                    Error = exchangeResponse.Error ?? "Failed to get access token",
                    ErrorDescription = exchangeResponse.ErrorDescription
                };
            }

            return new BearerTokenResult
            {
                AccessToken = AccessToken.Parse(exchangeResponse.AccessToken)
            };
        }

        return result;
    }
}
