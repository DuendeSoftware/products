// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default refresh token service
/// </summary>
public class DefaultRefreshTokenService : IRefreshTokenService
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The refresh token store
    /// </summary>
    protected IRefreshTokenStore RefreshTokenStore { get; }

    /// <summary>
    /// The profile service
    /// </summary>
    protected IProfileService Profile { get; }

    /// <summary>
    /// The clock
    /// </summary>
    protected IClock Clock { get; }

    /// <summary>
    /// The persistent grant options
    /// </summary>
    protected PersistentGrantOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRefreshTokenService" /> class.
    /// </summary>
    /// <param name="refreshTokenStore">The refresh token store</param>
    /// <param name="profile"></param>
    /// <param name="clock">The clock</param>
    /// <param name="options">The persistent grant options</param>
    /// <param name="logger">The logger</param>
    public DefaultRefreshTokenService(
        IRefreshTokenStore refreshTokenStore,
        IProfileService profile,
        IClock clock,
        PersistentGrantOptions options,
        ILogger<DefaultRefreshTokenService> logger)
    {
        RefreshTokenStore = refreshTokenStore;
        Profile = profile;
        Clock = clock;
        Options = options;

        Logger = logger;
    }

    /// <summary>
    /// Validates a refresh token
    /// </summary>
    /// <param name="tokenHandle">The token handle.</param>
    /// <param name="client">The client.</param>
    /// <returns></returns>
    public virtual async Task<TokenValidationResult> ValidateRefreshTokenAsync(string tokenHandle, Client client)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultRefreshTokenService.ValidateRefreshToken");

        var invalidGrant = new TokenValidationResult
        {
            IsError = true,
            Error = OidcConstants.TokenErrors.InvalidGrant
        };

        Logger.LogTrace("Start refresh token validation");

        /////////////////////////////////////////////
        // check if refresh token is valid
        /////////////////////////////////////////////
        var refreshToken = await RefreshTokenStore.GetRefreshTokenAsync(tokenHandle);
        if (refreshToken == null)
        {
            Logger.LogWarning("Invalid refresh token");
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if refresh token has expired
        /////////////////////////////////////////////
        if (refreshToken.CreationTime.HasExceeded(refreshToken.Lifetime, Clock.UtcNow.UtcDateTime))
        {
            Logger.LogWarning("Refresh token has expired.");
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if client belongs to requested refresh token
        /////////////////////////////////////////////
        if (client.ClientId != refreshToken.ClientId)
        {
            Logger.LogError("{0} tries to refresh token belonging to {1}", client.ClientId, refreshToken.ClientId);
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if client still has offline_access scope
        /////////////////////////////////////////////
        if (!client.AllowOfflineAccess)
        {
            Logger.LogError("{clientId} does not have access to offline_access scope anymore", client.ClientId);
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if refresh token has been consumed
        /////////////////////////////////////////////
        if (refreshToken.ConsumedTime.HasValue)
        {
            if ((await AcceptConsumedTokenAsync(refreshToken)) == false)
            {
                Logger.LogWarning("Rejecting refresh token because it has been consumed already.");
                return invalidGrant;
            }
        }

        /////////////////////////////////////////////
        // make sure user is enabled
        /////////////////////////////////////////////
        var isActiveCtx = new IsActiveContext(
            refreshToken.Subject,
            client,
            IdentityServerConstants.ProfileIsActiveCallers.RefreshTokenValidation);

        await Profile.IsActiveAsync(isActiveCtx);

        if (isActiveCtx.IsActive == false)
        {
            Logger.LogError("{subjectId} has been disabled", refreshToken.Subject.GetSubjectId());
            return invalidGrant;
        }

        return new TokenValidationResult
        {
            IsError = false,
            RefreshToken = refreshToken,
            Client = client
        };
    }

    /// <summary>
    /// Callback to decide if an already consumed token should be accepted.
    /// </summary>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    protected virtual Task<bool> AcceptConsumedTokenAsync(RefreshToken refreshToken)
    {
        // by default we will not accept consumed tokens
        // change the behavior here to implement a time window
        // you can also implement additional revocation logic here
        return Task.FromResult(false);
    }

    /// <summary>
    /// Creates the refresh token.
    /// </summary>
    /// <returns>
    /// The refresh token handle
    /// </returns>
    public virtual async Task<string> CreateRefreshTokenAsync(RefreshTokenCreationRequest request)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultRefreshTokenService.CreateRefreshToken");

        Logger.LogDebug("Creating refresh token");

        int lifetime;
        if (request.Client.RefreshTokenExpiration == TokenExpiration.Absolute)
        {
            Logger.LogDebug("Setting an absolute lifetime: {absoluteLifetime}",
                request.Client.AbsoluteRefreshTokenLifetime);
            lifetime = request.Client.AbsoluteRefreshTokenLifetime;
        }
        else
        {
            lifetime = request.Client.SlidingRefreshTokenLifetime;
            if (request.Client.AbsoluteRefreshTokenLifetime > 0 && lifetime > request.Client.AbsoluteRefreshTokenLifetime)
            {
                Logger.LogWarning(
                    "Client {clientId}'s configured " + nameof(request.Client.SlidingRefreshTokenLifetime) +
                    " of {slidingLifetime} exceeds its " + nameof(request.Client.AbsoluteRefreshTokenLifetime) +
                    " of {absoluteLifetime}. The refresh_token's sliding lifetime will be capped to the absolute lifetime",
                    request.Client.ClientId, lifetime, request.Client.AbsoluteRefreshTokenLifetime);
                lifetime = request.Client.AbsoluteRefreshTokenLifetime;
            }

            Logger.LogDebug("Setting a sliding lifetime: {slidingLifetime}", lifetime);
        }

        var refreshToken = new RefreshToken
        {
            Subject = request.Subject,
            SessionId = request.AccessToken.SessionId,
            ClientId = request.Client.ClientId,
            Description = request.Description,
            AuthorizedScopes = request.AuthorizedScopes,
            AuthorizedResourceIndicators = request.AuthorizedResourceIndicators,
            ProofType = request.ProofType,

            CreationTime = Clock.UtcNow.UtcDateTime,
            Lifetime = lifetime,
        };
        refreshToken.SetAccessToken(request.AccessToken, request.RequestedResourceIndicator);

        var handle = await RefreshTokenStore.StoreRefreshTokenAsync(refreshToken);
        return handle;
    }

    /// <summary>
    /// Updates the refresh token.
    /// </summary>
    /// <returns>
    /// The refresh token handle
    /// </returns>
    public virtual async Task<string> UpdateRefreshTokenAsync(RefreshTokenUpdateRequest request)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenCreationService.UpdateRefreshToken");

        Logger.LogDebug("Updating refresh token");

        var handle = request.Handle;
        var needsCreate = false;
        var needsUpdate = request.MustUpdate;

        if (request.Client.RefreshTokenUsage == TokenUsage.OneTimeOnly)
        {

            if (Options.DeleteOneTimeOnlyRefreshTokensOnUse)
            {
                Logger.LogDebug("Token usage is one-time only and refresh behavior is delete. Deleting current handle, and generating new handle");

                await RefreshTokenStore.RemoveRefreshTokenAsync(handle);
            }
            else
            {
                Logger.LogDebug("Token usage is one-time only and refresh behavior is mark as consumed. Setting current handle as consumed, and generating new handle");

                // flag as consumed
                if (request.RefreshToken.ConsumedTime == null)
                {
                    request.RefreshToken.ConsumedTime = Clock.UtcNow.UtcDateTime;
                    await RefreshTokenStore.UpdateRefreshTokenAsync(handle, request.RefreshToken);
                }
            }

            // create new one
            needsCreate = true;
        }

        if (request.Client.RefreshTokenExpiration == TokenExpiration.Sliding)
        {
            Logger.LogDebug("Refresh token expiration is sliding - extending lifetime");

            // if absolute exp > 0, make sure we don't exceed absolute exp
            // if absolute exp = 0, allow indefinite slide
            var currentLifetime = request.RefreshToken.CreationTime.GetLifetimeInSeconds(Clock.UtcNow.UtcDateTime);
            Logger.LogDebug("Current lifetime: {currentLifetime}", currentLifetime.ToString());

            var newLifetime = currentLifetime + request.Client.SlidingRefreshTokenLifetime;
            Logger.LogDebug("New lifetime: {slidingLifetime}", newLifetime.ToString());

            // zero absolute refresh token lifetime represents unbounded absolute lifetime
            // if absolute lifetime > 0, cap at absolute lifetime
            if (request.Client.AbsoluteRefreshTokenLifetime > 0 && newLifetime > request.Client.AbsoluteRefreshTokenLifetime)
            {
                newLifetime = request.Client.AbsoluteRefreshTokenLifetime;
                Logger.LogDebug("New lifetime exceeds absolute lifetime, capping it to {newLifetime}",
                    newLifetime.ToString());
            }

            request.RefreshToken.Lifetime = newLifetime;
            needsUpdate = true;
        }

        if (needsCreate)
        {
            // set it to null so that we save non-consumed token
            request.RefreshToken.ConsumedTime = null;
            handle = await RefreshTokenStore.StoreRefreshTokenAsync(request.RefreshToken);
            Logger.LogDebug("Created refresh token in store");
        }
        else if (needsUpdate)
        {
            await RefreshTokenStore.UpdateRefreshTokenAsync(handle, request.RefreshToken);
            Logger.LogDebug("Updated refresh token in store");
        }
        else
        {
            Logger.LogDebug("No updates to refresh token done");
        }

        return handle;
    }
}
