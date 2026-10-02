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

    /// <inheritdoc/>
    public virtual async Task<TokenRevocationResponse> ProcessAsync(TokenRevocationRequestValidationResult validationResult, Ct ct)
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
            Logger.HintWasForAccessToken();
            response.Success = await RevokeAccessTokenAsync(validationResult, ct);
        }
        else if (validationResult.TokenTypeHint == Constants.TokenTypeHints.RefreshToken)
        {
            Logger.HintWasForRefreshToken();
            response.Success = await RevokeRefreshTokenAsync(validationResult, ct);
        }
        else
        {
            Logger.NoHintForTokenType();

            response.Success = await RevokeAccessTokenAsync(validationResult, ct);

            if (!response.Success)
            {
                response.Success = await RevokeRefreshTokenAsync(validationResult, ct);
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
    protected virtual async Task<bool> RevokeAccessTokenAsync(TokenRevocationRequestValidationResult validationResult, Ct ct)
    {
        var token = await ReferenceTokenStore.GetReferenceTokenAsync(validationResult.Token, ct);

        if (token != null)
        {
            if (token.ClientId == validationResult.Client.ClientId)
            {
                Logger.AccessTokenRevoked();
                await ReferenceTokenStore.RemoveReferenceTokenAsync(validationResult.Token, ct);
            }
            else
            {
                Logger.ClientDeniedFromRevokingAccessTokenBelongingTo(validationResult.Client.ClientId, token.ClientId);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Revoke refresh token only if it belongs to client doing the request
    /// </summary>
    protected virtual async Task<bool> RevokeRefreshTokenAsync(TokenRevocationRequestValidationResult validationResult, Ct ct)
    {
        var token = await RefreshTokenStore.GetRefreshTokenAsync(validationResult.Token, ct);

        if (token != null)
        {
            if (token.ClientId == validationResult.Client.ClientId)
            {
                Logger.RefreshTokenRevoked();
                await RefreshTokenStore.RemoveRefreshTokenAsync(validationResult.Token, ct);
                await ReferenceTokenStore.RemoveReferenceTokensAsync(token.SubjectId, token.ClientId, token.SessionId, ct);
            }
            else
            {
                Logger.ClientDeniedFromRevokingARefreshTokenBelonging(validationResult.Client.ClientId, token.ClientId);
            }

            return true;
        }

        return false;
    }
}
