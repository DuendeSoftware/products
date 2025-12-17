// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Provides access to the HybridCache implementation used for DPoP proof token replay detection.
/// </summary>
internal class DPoPHybridCacheProvider(IServiceProvider serviceProvider)
{
    public HybridCache GetCache()
    {
        var cache = serviceProvider.GetKeyedService<HybridCache>(ServiceProviderKeys.ProofTokenReplayHybridCache);
        if (cache == null)
        {
            throw new InvalidOperationException(
                "Replay detection is enabled, but no HybridCache implementation is registered for the key " +
                $"{nameof(ServiceProviderKeys.ProofTokenReplayHybridCache)}. Either disable replay detection by setting " +
                $"the {nameof(DPoPOptions.EnableReplayDetection)} option to false, or register a HybridCache implementation." +
                "The default hybrid cache can be registered by calling " +
                $"AddKeyedHybridCache({nameof(ServiceProviderKeys)}.{nameof(ServiceProviderKeys.ProofTokenReplayHybridCache)}, ...).");
        }
        return cache;
    }
}
