// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Spaces;

/// <summary>
/// Options for configuring Spaces behavior.
/// </summary>
public sealed class SpacesOptions
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

    /// <summary>
    /// Gets or sets whether the app's data protection is partitioned by space. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, protectors from the app's non-keyed <c>IDataProtectionProvider</c> are bound to
    /// the resolved space, and using them without a resolved space throws. Turning this off after
    /// running with it on is unsupported. Use <c>GetUnpartitioned()</c> (see
    /// <see cref="SpacesDataProtectionExtensions"/>) for data shared across spaces. Keyed providers and
    /// providers set on authentication scheme options are not partitioned.
    /// </remarks>
    public bool PartitionDataProtection { get; set; } = true;
}
