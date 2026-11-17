// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.Spaces;

/// <summary>
/// Options for configuring Spaces behavior.
/// </summary>
public sealed class SpacesOptions
{
    /// <summary>
    /// Gets or sets the path prefix used to identify space-specific routes.
    /// Defaults to <c>/t</c>. Must be lowercase: Spaces only resolves lowercase space paths,
    /// and the host fails to start when the prefix contains uppercase letters.
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

    /// <summary>
    /// Gets or sets whether <c>IdentityServerOptions.IssuerUri</c> may be set when Spaces is used. Defaults to <c>false</c>,
    /// and the host fails to start when <c>IssuerUri</c> is set.
    /// </summary>
    /// <remarks>
    /// Without <c>IssuerUri</c>, each space has its own issuer. With it, every space has the same issuer.
    /// If the spaces also share signing keys (a signing credential added with <c>AddSigningCredential</c> or
    /// <c>AddDeveloperSigningCredential</c>, or the file-system key store), a token issued in one space can be
    /// accepted in another space when its other checks, such as the audience and client, also pass there.
    /// Only set this to <c>true</c> when that is intended.
    /// </remarks>
    public bool AllowSharedIssuerUri { get; set; }
}
