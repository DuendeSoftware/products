// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal class ResourceLoadedTracker
{
    private const int MaxResourceTrackedCount = 100;

    private int _resourceCount;
    private readonly ConcurrentDictionary<string, TrackedResource> _resources = new();

    public void TrackResources(Resources resources)
    {
        foreach (var resourcesApiResource in resources.ApiResources)
        {
            TrackResource(resourcesApiResource);
        }

        foreach (var resourcesIdentityResource in resources.IdentityResources)
        {
            TrackResource(resourcesIdentityResource);
        }

        foreach (var resourcesApiScope in resources.ApiScopes)
        {
            TrackResource(resourcesApiScope);
        }
    }

    public IReadOnlyDictionary<string, TrackedResource> Resources => _resources;

    private void TrackResource(ApiResource apiResource)
    {
        if (_resourceCount >= MaxResourceTrackedCount)
        {
            return;
        }

        if (_resources.ContainsKey($"ApiResource:{apiResource.Name}"))
        {
            return;
        }

        var resource = new TrackedResource("ApiResource",
            apiResource.Name,
            apiResource.RequireResourceIndicator,
            apiResource.ApiSecrets?.Select(secret => secret.Type).Distinct());

        if (_resources.TryAdd($"ApiResource:{apiResource.Name}", resource))
        {
            Interlocked.Increment(ref _resourceCount);
        }
    }

    private void TrackResource(IdentityResource identityResource)
    {
        if (_resourceCount >= MaxResourceTrackedCount)
        {
            return;
        }

        if (_resources.ContainsKey($"IdentityResource:{identityResource.Name}"))
        {
            return;
        }

        var resource = new TrackedResource("IdentityResource",
            identityResource.Name,
            null,
            null);

        if (_resources.TryAdd($"IdentityResource:{identityResource.Name}", resource))
        {
            Interlocked.Increment(ref _resourceCount);
        }
    }

    private void TrackResource(ApiScope apiScope)
    {
        if (_resourceCount >= MaxResourceTrackedCount)
        {
            return;
        }

        if (_resources.ContainsKey($"ApiScope:{apiScope.Name}"))
        {
            return;
        }

        var resource = new TrackedResource("ApiScope",
            apiScope.Name,
            null,
            null);

        if (_resources.TryAdd($"ApiScope:{apiScope.Name}", resource))
        {
            Interlocked.Increment(ref _resourceCount);
        }
    }
}

internal record TrackedResource(
    string Type,
    string Name,
    bool? ResourceIndicatorRequired,
    IEnumerable<string>? SecretTypes);
