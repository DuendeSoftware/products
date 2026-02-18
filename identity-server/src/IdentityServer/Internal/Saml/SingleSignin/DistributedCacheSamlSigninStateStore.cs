// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text.Json;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class DistributedCacheSamlSigninStateStore(IDistributedCache cache) : ISamlSigninStateStore
{
    private const string KeyPrefix = "saml-signin-state:";
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    public async Task<StateId> StoreSigninRequestStateAsync(SamlAuthenticationState state, CancellationToken ct = default)
    {
        var stateId = StateId.NewId();
        var key = GetKey(stateId);
        var json = JsonSerializer.Serialize(state);

        await cache.SetStringAsync(key, json, CacheOptions, ct);

        return stateId;
    }

    public async Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(StateId stateId, CancellationToken ct = default)
    {
        var key = GetKey(stateId);
        var json = await cache.GetStringAsync(key, ct);

        if (json == null)
        {
            return null;
        }

        await cache.RemoveAsync(key, ct);

        return JsonSerializer.Deserialize<SamlAuthenticationState>(json);
    }

    public async Task UpdateSigninRequestStateAsync(StateId stateId, SamlAuthenticationState state, CancellationToken ct = default)
    {
        var key = GetKey(stateId);
        var json = JsonSerializer.Serialize(state);

        await cache.SetStringAsync(key, json, CacheOptions, ct);
    }

    private static string GetKey(StateId stateId) => $"{KeyPrefix}{stateId.Value}";
}
