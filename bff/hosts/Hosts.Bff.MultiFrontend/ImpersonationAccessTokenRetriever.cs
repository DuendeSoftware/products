// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;

// TODO: remove pragma?
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Bff;
#pragma warning restore IDE0130

public class ImpersonationAccessTokenRetriever(IAccessTokenRetriever inner) : IAccessTokenRetriever
{
    // BFF doesn't limit access token length. This retriever gets its token without ATM,
    // so it's responsible for rejecting tokens that are too long.
    private const int MaxAccessTokenLength = 32 * 1024;

    public async Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, Ct ct = default)
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

            if (exchangeResponse.AccessToken.Length > MaxAccessTokenLength)
            {
                return new AccessTokenRetrievalError
                {
                    Error = "Token exchange failed. Access token is too long"
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
