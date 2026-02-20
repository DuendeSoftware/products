// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Default refresh token store.
/// </summary>
public class DefaultRefreshTokenStore : DefaultGrantStore<RefreshToken>, IRefreshTokenStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRefreshTokenStore"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="handleGenerationService">The handle generation service.</param>
    /// <param name="logger">The logger.</param>
    public DefaultRefreshTokenStore(
        IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        IHandleGenerationService handleGenerationService,
        ILogger<DefaultRefreshTokenStore> logger)
        : base(IdentityServerConstants.PersistedGrantTypes.RefreshToken, store, serializer, handleGenerationService, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<string> StoreRefreshTokenAsync(RefreshToken refreshToken, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultRefreshTokenStore.StoreRefreshTokenAsync");

        return await CreateItemAsync(refreshToken, refreshToken.ClientId, refreshToken.SubjectId, refreshToken.SessionId, refreshToken.Description, refreshToken.CreationTime, refreshToken.Lifetime, ct);
    }

    /// <inheritdoc/>
    public Task UpdateRefreshTokenAsync(string handle, RefreshToken refreshToken, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultRefreshTokenStore.UpdateRefreshToken");

        return StoreItemAsync(handle, refreshToken, refreshToken.ClientId, refreshToken.SubjectId, refreshToken.SessionId, refreshToken.Description, refreshToken.CreationTime, refreshToken.CreationTime.AddSeconds(refreshToken.Lifetime), refreshToken.ConsumedTime, ct);
    }

    /// <inheritdoc/>
    public Task<RefreshToken> GetRefreshTokenAsync(string refreshTokenHandle, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultRefreshTokenStore.GetRefreshToken");

        return GetItemAsync(refreshTokenHandle, ct);
    }

    /// <inheritdoc/>
    public Task RemoveRefreshTokenAsync(string refreshTokenHandle, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultRefreshTokenStore.RemoveRefreshToken");

        return RemoveItemAsync(refreshTokenHandle, ct);
    }

    /// <inheritdoc/>
    public Task RemoveRefreshTokensAsync(string subjectId, string clientId, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultRefreshTokenStore.RemoveRefreshTokens");

        return RemoveAllAsync(subjectId, clientId, ct: ct);
    }
}
