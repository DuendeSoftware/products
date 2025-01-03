// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Duende.AccessTokenManagement;
using Duende.Bff.Logging;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Transforms;

namespace Duende.Bff.Yarp;

/// <summary>
/// Adds an access token to outgoing requests
/// </summary>
public class AccessTokenRequestTransform : RequestTransform
{
    private readonly IDPoPProofService _dPoPProofService;
    private readonly ILogger<AccessTokenRequestTransform> _logger;
    private readonly AccessTokenResult _token;
    private readonly string? _routeId;
    private readonly TokenType? _tokenType;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="proofService"></param>
    /// <param name="logger"></param>
    /// <param name="accessToken"></param>
    /// <param name="routeId"></param>
    /// <param name="tokenType"></param>
    public AccessTokenRequestTransform(
        IDPoPProofService proofService,
        ILogger<AccessTokenRequestTransform> logger,
        AccessTokenResult accessToken,
        string? routeId = null,
        TokenType? tokenType = null)
    {
        _dPoPProofService = proofService;
        _logger = logger;
        _token = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        _routeId = routeId;
        _tokenType = tokenType;
    }

    /// <inheritdoc />
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        switch (_token)
        {
            case BearerTokenResult bearerToken:
                ApplyBearerToken(context, bearerToken);
                break;
            case DPoPTokenResult dpopToken:
                await ApplyDPoPToken(context, dpopToken);
                break;
            case AccessTokenRetrievalError tokenError:
                ApplyError(context, tokenError, _routeId ?? "Unknown Route", _tokenType);
                break;
            case NoAccessTokenResult noToken:
                break;
            default:
                break;
        }
    }

    private void ApplyError(RequestTransformContext context, AccessTokenRetrievalError tokenError, string routeId, TokenType? tokenType)
    {
        // short circuit forwarder and return 401
        context.HttpContext.Response.StatusCode = 401;

        _logger.AccessTokenMissing(tokenType?.ToString() ?? "Unknown token type", routeId, tokenError.Error);
    }

    private void ApplyBearerToken(RequestTransformContext context, BearerTokenResult token)
    {
        context.ProxyRequest.Headers.Authorization =
            new AuthenticationHeaderValue(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, token.AccessToken);
    }

    private async Task ApplyDPoPToken(RequestTransformContext context, DPoPTokenResult token)
    {
        ArgumentNullException.ThrowIfNull(token.DPoPJsonWebKey, nameof(token.DPoPJsonWebKey));

        var baseUri = new Uri(context.DestinationPrefix);
        var proofToken = await _dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
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