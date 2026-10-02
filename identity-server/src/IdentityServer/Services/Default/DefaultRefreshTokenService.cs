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
    /// The time provider
    /// </summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>
    /// The persistent grant options
    /// </summary>
    protected PersistentGrantOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRefreshTokenService" /> class.
    /// </summary>
    /// <param name="refreshTokenStore">The refresh token store</param>
    /// <param name="profile"></param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="options">The persistent grant options</param>
    /// <param name="logger">The logger</param>
    public DefaultRefreshTokenService(
        IRefreshTokenStore refreshTokenStore,
        IProfileService profile,
        TimeProvider timeProvider,
        PersistentGrantOptions options,
        ILogger<DefaultRefreshTokenService> logger)
    {
        RefreshTokenStore = refreshTokenStore;
        Profile = profile;
        TimeProvider = timeProvider;
        Options = options;

        Logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<TokenValidationResult> ValidateRefreshTokenAsync(string tokenHandle, Client client, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultRefreshTokenService.ValidateRefreshToken");

        var invalidGrant = new TokenValidationResult
        {
            IsError = true,
            Error = OidcConstants.TokenErrors.InvalidGrant
        };

        Logger.StartRefreshTokenValidation();

        /////////////////////////////////////////////
        // check if refresh token is valid
        /////////////////////////////////////////////
        var refreshToken = await RefreshTokenStore.GetRefreshTokenAsync(tokenHandle, ct);
        if (refreshToken == null)
        {
            Logger.InvalidRefreshToken();
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if refresh token has expired
        /////////////////////////////////////////////
        if (refreshToken.CreationTime.HasExceeded(refreshToken.Lifetime, TimeProvider.GetUtcNow().UtcDateTime))
        {
            Logger.RefreshTokenHasExpired();
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if client belongs to requested refresh token
        /////////////////////////////////////////////
        if (client.ClientId != refreshToken.ClientId)
        {
            Logger.ClientIdTriesToRefreshTokenBelongingToRefreshTokenClientId(client.ClientId, refreshToken.ClientId);
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if client still has offline_access scope
        /////////////////////////////////////////////
        if (!client.AllowOfflineAccess)
        {
            Logger.ClientIdDoesNotHaveAccessToOfflineAccess(client.ClientId);
            return invalidGrant;
        }

        /////////////////////////////////////////////
        // check if refresh token has been consumed
        /////////////////////////////////////////////
        if (refreshToken.ConsumedTime.HasValue)
        {
            if ((await AcceptConsumedTokenAsync(refreshToken)) == false)
            {
                Logger.RejectingRefreshTokenBecauseItHasBeenConsumed();
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

        await Profile.IsActiveAsync(isActiveCtx, ct);

        if (isActiveCtx.IsActive == false)
        {
            Logger.SubjectIdHasBeenDisabled(refreshToken.Subject.GetSubjectId());
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
    protected virtual Task<bool> AcceptConsumedTokenAsync(RefreshToken refreshToken) =>
        // by default we will not accept consumed tokens
        // change the behavior here to implement a time window
        // you can also implement additional revocation logic here
        Task.FromResult(false);

    /// <summary>
    /// Creates the refresh token.
    /// </summary>
    /// <returns>
    /// The refresh token handle
    /// </returns>
    public virtual async Task<string> CreateRefreshTokenAsync(RefreshTokenCreationRequest request, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultRefreshTokenService.CreateRefreshToken");

        Logger.CreatingRefreshToken();

        int lifetime;
        if (request.Client.RefreshTokenExpiration == TokenExpiration.Absolute)
        {
            Logger.SettingAnAbsoluteLifetimeAbsoluteLifetime(request.Client.AbsoluteRefreshTokenLifetime);
            lifetime = request.Client.AbsoluteRefreshTokenLifetime;
        }
        else
        {
            lifetime = request.Client.SlidingRefreshTokenLifetime;
            if (request.Client.AbsoluteRefreshTokenLifetime > 0 && lifetime > request.Client.AbsoluteRefreshTokenLifetime)
            {
                Logger.ClientClientIdSConfiguredNameofRequestClientSlidingRefreshTokenLifetime(request.Client.ClientId, lifetime, request.Client.AbsoluteRefreshTokenLifetime);
                lifetime = request.Client.AbsoluteRefreshTokenLifetime;
            }

            Logger.SettingASlidingLifetimeSlidingLifetime(lifetime);
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

            CreationTime = TimeProvider.GetUtcNow().UtcDateTime,
            Lifetime = lifetime,
        };
        refreshToken.SetAccessToken(request.AccessToken, request.RequestedResourceIndicator);

        var handle = await RefreshTokenStore.StoreRefreshTokenAsync(refreshToken, ct);
        return handle;
    }

    /// <summary>
    /// Updates the refresh token.
    /// </summary>
    /// <returns>
    /// The refresh token handle
    /// </returns>
    public virtual async Task<string> UpdateRefreshTokenAsync(RefreshTokenUpdateRequest request, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenCreationService.UpdateRefreshToken");

        Logger.UpdatingRefreshToken();

        var handle = request.Handle;
        var needsCreate = false;
        var needsUpdate = request.MustUpdate;

        if (request.Client.RefreshTokenUsage == TokenUsage.OneTimeOnly)
        {

            if (Options.DeleteOneTimeOnlyRefreshTokensOnUse)
            {
                Logger.TokenUsageIsOneTimeOnlyAndRefresh();

                await RefreshTokenStore.RemoveRefreshTokenAsync(handle, ct);
            }
            else
            {
                Logger.TokenUsageIsOneTimeOnlyAndRefresh2();

                // flag as consumed
                if (request.RefreshToken.ConsumedTime == null)
                {
                    request.RefreshToken.ConsumedTime = TimeProvider.GetUtcNow().UtcDateTime;
                    await RefreshTokenStore.UpdateRefreshTokenAsync(handle, request.RefreshToken, ct);
                }
            }

            // create new one
            needsCreate = true;
        }

        if (request.Client.RefreshTokenExpiration == TokenExpiration.Sliding)
        {
            Logger.RefreshTokenExpirationIsSlidingExtendingLifetime();

            // if absolute exp > 0, make sure we don't exceed absolute exp
            // if absolute exp = 0, allow indefinite slide
            var currentLifetime = request.RefreshToken.CreationTime.GetLifetimeInSeconds(TimeProvider.GetUtcNow().UtcDateTime);
            Logger.CurrentLifetimeCurrentLifetime(currentLifetime);

            var newLifetime = currentLifetime + request.Client.SlidingRefreshTokenLifetime;
            Logger.NewLifetimeSlidingLifetime(newLifetime);

            // zero absolute refresh token lifetime represents unbounded absolute lifetime
            // if absolute lifetime > 0, cap at absolute lifetime
            if (request.Client.AbsoluteRefreshTokenLifetime > 0 && newLifetime > request.Client.AbsoluteRefreshTokenLifetime)
            {
                newLifetime = request.Client.AbsoluteRefreshTokenLifetime;
                Logger.NewLifetimeExceedsAbsoluteLifetimeCappingItTo(newLifetime);
            }

            request.RefreshToken.Lifetime = newLifetime;
            needsUpdate = true;
        }

        if (needsCreate)
        {
            // set it to null so that we save non-consumed token
            request.RefreshToken.ConsumedTime = null;
            handle = await RefreshTokenStore.StoreRefreshTokenAsync(request.RefreshToken, ct);
            Logger.CreatedRefreshTokenInStore();
        }
        else if (needsUpdate)
        {
            await RefreshTokenStore.UpdateRefreshTokenAsync(handle, request.RefreshToken, ct);
            Logger.UpdatedRefreshTokenInStore();
        }
        else
        {
            Logger.NoUpdatesToRefreshTokenDone();
        }

        return handle;
    }
}
