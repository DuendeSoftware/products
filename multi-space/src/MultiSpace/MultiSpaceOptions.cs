// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.MultiSpace;

/// <summary>
/// Options for configuring multi-space behavior.
/// </summary>
public sealed class MultiSpaceOptions
{
    /// <summary>
    /// Gets or sets the path prefix used to identify space-specific routes.
    /// Defaults to <c>/t</c>.
    /// </summary>
    public PathString? SpacePathPrefix { get; set; } = "/t";

    /// <summary>
    /// Gets or sets the duration for which local (in-process) cache entries are valid.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan LocalCacheExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the expiration duration for distributed cache entries.
    /// Defaults to 30 minutes.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets whether unresolvable requests fall back to <see cref="SpaceId.Default"/> (pool 0).
    /// When <c>true</c>, requests that don't match any space are routed to the default pool.
    /// When <c>false</c> (default), unmatched requests receive a 404 response.
    /// </summary>
    public bool FallbackToDefault { get; set; }
}
