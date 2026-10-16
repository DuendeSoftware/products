// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Spaces;

/// <summary>
/// Keys used to resolve Spaces keyed services from dependency injection.
/// </summary>
public static class SpacesServiceProviderKeys
{
    /// <summary>
    /// Service key for the <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> instance
    /// Spaces uses to cache space resolution and routing lookups. Register a keyed
    /// <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> under this key to give Spaces
    /// a dedicated cache instance; otherwise Spaces registers one with default settings.
    /// </summary>
    public const string SpacesCache = nameof(SpacesCache);
}
