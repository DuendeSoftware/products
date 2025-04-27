// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Default revocation response generator
/// </summary>
/// <seealso cref="ITokenRevocationResponseGenerator" />
public class TokenRevocationResponseGenerator : ITokenRevocationResponseGenerator
{
    /// <summary>
    /// Gets the reference token store.
    /// </summary>
    /// <value>
    /// The reference token store.
    /// </value>
    protected readonly IReferenceTokenStore ReferenceTokenStore;

    /// <summary>
    /// Gets the refresh token store.
    /// </summary>
    /// <value>
    /// The refresh token store.
    /// </value>
    protected readonly IRefreshTokenStore RefreshTokenStore;

    /// <summary>
    /// Gets the logger.
    /// </summary>
    /// <value>
    /// The logger.
    /// </value>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenRevocationResponseGenerator" /> class.
    /// </summary>
    /// <param name="referenceTokenStore">The reference token store.</param>
    /// <param name="refreshTokenStore">The refresh token store.</param>
    /// <param name="logger">The logger.</param>
    public TokenRevocationResponseGenerator(IReferenceTokenStore referenceTokenStore, IRefreshTokenStore refreshTokenStore, ILogger<TokenRevocationResponseGenerator> logger)
    {
        ReferenceTokenStore = referenceTokenStore;
        RefreshTokenStore = refreshTokenStore;
        Logger = logger;
    }

    /// <summary>
    /// Creates the revocation endpoint response and processes the revocation request.
    /// </summary>
    /// <param name="validationResult">The userinfo request validation result.</param>
    public virtual async Task<TokenRevocationResponse> ProcessAsync(TokenRevocationRequestValidationResult validationResult)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenRevocationResponseGenerator.Process");

        var response = new TokenRevocationResponse
        {
            Success = false,
            TokenType = validationResult.TokenTypeHint
        };

        // revoke tokens
        if (validationResult.TokenTypeHint == Constants.TokenTypeHints.AccessToken)
        {
            Logger.LogTrace("Hint was for access token");
            response.Success = await RevokeAccessTokenAsync(validationResult);
        }
        else if (validationResult.TokenTypeHint == Constants.TokenTypeHints.RefreshToken)
        {
            Logger.LogTrace("Hint was for refresh token");
            response.Success = await RevokeRefreshTokenAsync(validationResult);
        }
        else
        {
            Logger.LogTrace("No hint for token type");

            response.Success = await RevokeAccessTokenAsync(validationResult);

            if (!response.Success)
            {
                response.Success = await RevokeRefreshTokenAsync(validationResult);
                response.TokenType = Constants.TokenTypeHints.RefreshToken;
            }
            else
            {
                response.TokenType = Constants.TokenTypeHints.AccessToken;
            }
        }

        return response;
    }

    /// <summary>
    /// Revoke access token only if it belongs to client doing the request.
    /// </summary>
    protected virtual async Task<bool> RevokeAccessTokenAsync(TokenRevocationRequestValidationResult validationResult)
    {
        var token = await ReferenceTokenStore.GetReferenceTokenAsync(validationResult.Token);

        if (token != null)
        {
            if (token.ClientId == validationResult.Client.ClientId)
            {
                Logger.LogDebug("Access token revoked");
                await ReferenceTokenStore.RemoveReferenceTokenAsync(validationResult.Token);
            }
            else
            {
                Logger.LogWarning("Client {clientId} denied from revoking access token belonging to Client {tokenClientId}", validationResult.Client.ClientId, token.ClientId);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Revoke refresh token only if it belongs to client doing the request
    /// </summary>
    protected virtual async Task<bool> RevokeRefreshTokenAsync(TokenRevocationRequestValidationResult validationResult)
    {
        var token = await RefreshTokenStore.GetRefreshTokenAsync(validationResult.Token);

        if (token != null)
        {
            if (token.ClientId == validationResult.Client.ClientId)
            {
                Logger.LogDebug("Refresh token revoked");
                await RefreshTokenStore.RemoveRefreshTokenAsync(validationResult.Token);
                await ReferenceTokenStore.RemoveReferenceTokensAsync(token.SubjectId, token.ClientId, token.SessionId);
            }
            else
            {
                Logger.LogWarning("Client {clientId} denied from revoking a refresh token belonging to Client {tokenClientId}", validationResult.Client.ClientId, token.ClientId);
            }

            return true;
        }

        return false;
    }
}
