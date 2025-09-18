// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff;

internal static class HttpContextExtensions
{
    public static void CheckForBffMiddleware(this HttpContext context, BffOptions options)
    {
        if (options.EnforceBffMiddleware)
        {
            var found = context.Items.TryGetValue(Constants.BffMiddlewareMarker, out _);
            if (!found)
            {
                throw new InvalidOperationException(
                    "The BFF middleware is missing in the pipeline. Add 'app.UseBff' after 'app.UseRouting' but before 'app.UseAuthorization'");
            }
        }
    }

    public static bool CheckAntiForgeryHeader(this HttpContext context, BffOptions options)
    {
        var antiForgeryHeader = context.Request.Headers[options.AntiForgeryHeaderName].FirstOrDefault();
        return antiForgeryHeader != null && antiForgeryHeader == options.AntiForgeryHeaderValue;
    }

    public static async Task<AccessTokenResult> GetManagedAccessToken(this HttpContext context, TokenType tokenType, bool optional = false, UserTokenRequestParameters? userAccessTokenParameters = null)
    {
        // Retrieve the appropriate type of access token (user vs client)
        var token = tokenType switch
        {
            TokenType.User => await context.GetUserAccessTokenAsync(userAccessTokenParameters),
            TokenType.Client => await context.GetClientAccessTokenAsync(),
            TokenType.UserOrClient => await GetUserOrClientAccessTokenAsync(context, userAccessTokenParameters),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenType), $"Unexpected TokenType: {tokenType}")
        };

        // Map the result onto the appropriate type of access token result (Bearer vs DPoP)
        return token switch
        {
            null or { AccessToken: null } =>
                optional ?
                    new NoAccessTokenResult() :
                    new NoAccessTokenReturnedError("Missing access token"),
            { AccessTokenType: OidcConstants.TokenResponse.BearerTokenType } =>
                new BearerTokenResult(token.AccessToken),
            { AccessTokenType: OidcConstants.TokenResponse.DPoPTokenType, DPoPJsonWebKey: not null } =>
                 new DPoPTokenResult(token.AccessToken, token.DPoPJsonWebKey),
            { AccessTokenType: OidcConstants.TokenResponse.DPoPTokenType, DPoPJsonWebKey: null } =>
                 new MissingDPopTokenError("Missing DPoP Json Web Key for DPoP token"),
            { AccessTokenType: string accessTokenType } =>
                new UnexpectedAccessTokenError($"Unexpected access token type: {accessTokenType} - should be one of 'DPoP' or 'Bearer'"),
            { AccessTokenType: null } =>
                // Fall back to bearer tokens when the access token type is absent.
                // In some edge cases, we've seen bearer tokens not have their type specified.
                // But that wouldn't be the case if you had a DPoP token.
                new BearerTokenResult(token.AccessToken)
        };

        static async Task<ClientCredentialsToken> GetUserOrClientAccessTokenAsync(HttpContext context, UserTokenRequestParameters? userAccessTokenParameters)
        {
            ClientCredentialsToken token = await context.GetUserAccessTokenAsync(userAccessTokenParameters);
            if (token.AccessToken == null)
            {
                token = await context.GetClientAccessTokenAsync();
            }
            return token;
        }
    }

    public static bool IsAjaxRequest(this HttpContext context)
    {
        if ("cors".Equals(context.Request.Headers["Sec-Fetch-Mode"].ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        if ("XMLHttpRequest".Equals(context.Request.Query["X-Requested-With"].ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        if ("XMLHttpRequest".Equals(context.Request.Headers["X-Requested-With"].ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
