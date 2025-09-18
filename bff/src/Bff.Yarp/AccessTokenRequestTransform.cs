// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Logging;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace Duende.Bff.Yarp;

/// <summary>
/// Adds an access token to outgoing requests
/// </summary>
public class AccessTokenRequestTransform(
    IOptions<BffOptions> options,
    IDPoPProofService proofService,
    ILogger<AccessTokenRequestTransform> logger) : RequestTransform
{

    /// <inheritdoc />
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint == null)
        {
            throw new InvalidOperationException("endpoint not found");
        }
        UserTokenRequestParameters? userAccessTokenParameters = null;

        context.HttpContext.RequestServices.CheckLicense();

        // Get the metadata
        var metadata =
            // Either from the endpoint directly, when using mapbff
            endpoint.Metadata.GetMetadata<BffRemoteApiEndpointMetadata>()
            // or from yarp
            ?? GetBffMetadataFromYarp(endpoint)
            ?? throw new InvalidOperationException("API endpoint is missing BFF metadata");

        if (metadata.BffUserAccessTokenParameters != null)
        {
            userAccessTokenParameters = metadata.BffUserAccessTokenParameters.ToUserAccessTokenRequestParameters();
        }

        if (context.HttpContext.RequestServices.GetRequiredService(metadata.AccessTokenRetriever)
            is not IAccessTokenRetriever accessTokenRetriever)
        {
            throw new InvalidOperationException("TokenRetriever is not an IAccessTokenRetriever");
        }

        var accessTokenContext = new AccessTokenRetrievalContext()
        {
            HttpContext = context.HttpContext,
            Metadata = metadata,
            UserTokenRequestParameters = userAccessTokenParameters,
            ApiAddress = new Uri(context.DestinationPrefix),
            LocalPath = context.HttpContext.Request.Path
        };
        var result = await accessTokenRetriever.GetAccessToken(accessTokenContext);

        switch (result)
        {
            case BearerTokenResult bearerToken:
                ApplyBearerToken(context, bearerToken);
                break;
            case DPoPTokenResult dpopToken:
                await ApplyDPoPToken(context, dpopToken);
                break;
            case AccessTokenRetrievalError tokenError:

                if (ShouldSignOutUser(tokenError, metadata))
                {
                    // see if we need to sign out
                    var authenticationSchemeProvider = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
                    // get rid of local cookie first
                    var signInScheme = await authenticationSchemeProvider.GetDefaultSignInSchemeAsync();
                    await context.HttpContext.SignOutAsync(signInScheme?.Name);
                }

                ApplyError(context, tokenError, metadata.RequiredTokenType);
                break;
            case NoAccessTokenResult noToken:
                break;
            default:
                break;
        }
    }

    private bool ShouldSignOutUser(AccessTokenRetrievalError tokenError, BffRemoteApiEndpointMetadata metadata)
    {
        if (tokenError is NoAccessTokenReturnedError && metadata.RequiredTokenType == TokenType.User ||
            metadata.RequiredTokenType == TokenType.UserOrClient)
        {
            if (!options.Value.RemoveSessionAfterRefreshTokenExpiration)
            {
                logger.FailedToRequestNewUserAccessToken(tokenError.Error);
                return false;
            }

            logger.UserSessionRevoked(tokenError.Error);
            return true;

        }

        return false;
    }

    private static BffRemoteApiEndpointMetadata? GetBffMetadataFromYarp(Endpoint endpoint)
    {
        var yarp = endpoint.Metadata.GetMetadata<RouteModel>();
        if (yarp == null)
        {
            return null;
        }

        TokenType? requiredTokenType = null;
        if (Enum.TryParse<TokenType>(yarp.Config?.Metadata?.GetValueOrDefault(Constants.Yarp.TokenTypeMetadata), true, out var type))
        {
            requiredTokenType = type;
        }

        return new BffRemoteApiEndpointMetadata()
        {
            OptionalUserToken = yarp.Config?.Metadata?.GetValueOrDefault(Constants.Yarp.OptionalUserTokenMetadata) == "true",
            RequiredTokenType = requiredTokenType
        };
    }

    private void ApplyError(RequestTransformContext context, AccessTokenRetrievalError tokenError, TokenType? tokenType)
    {
        // short circuit forwarder and return 401
        context.HttpContext.Response.StatusCode = 401;

        logger.AccessTokenMissing(tokenType?.ToString() ?? "Unknown token type", context.HttpContext.Request.Path, tokenError.Error);
    }

    private void ApplyBearerToken(RequestTransformContext context, BearerTokenResult token) => context.ProxyRequest.Headers.Authorization =
            new AuthenticationHeaderValue(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, token.AccessToken);

    private async Task ApplyDPoPToken(RequestTransformContext context, DPoPTokenResult token)
    {
        ArgumentNullException.ThrowIfNull(token.DPoPJsonWebKey, nameof(token.DPoPJsonWebKey));

        var baseUri = new Uri(context.DestinationPrefix);
        var proofToken = await proofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPJsonWebKey = token.DPoPJsonWebKey,
            Method = context.ProxyRequest.Method.ToString(),
            Url = new Uri(baseUri, context.Path).ToString()
        });
        if (proofToken != null)
        {
            context.ProxyRequest.Headers.Remove(OidcConstants.HttpHeaders.DPoP);
            context.ProxyRequest.Headers.Add(OidcConstants.HttpHeaders.DPoP, proofToken.ProofToken);
            context.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, token.AccessToken);
        }
        else
        {
            // The proof service can opt out of DPoP by returning null. If so,
            // we just use the access token as a bearer token.
            context.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, token.AccessToken);
        }
    }
}
