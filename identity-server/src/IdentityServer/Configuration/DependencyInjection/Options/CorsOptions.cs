// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for CORS
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// Gets or sets the name of the cors policy.
    /// </summary>
    /// <value>
    /// The name of the cors policy.
    /// </value>
    public string CorsPolicyName { get; set; } = Constants.IdentityServerName;

    /// <summary>
    /// The value to be used in the preflight `Access-Control-Max-Age` response header.
    /// </summary>
    public TimeSpan? PreflightCacheDuration { get; set; }

    /// <summary>
    /// Gets or sets the cors paths.
    /// </summary>
    /// <value>
    /// The cors paths.
    /// </value>
    public ICollection<PathString> CorsPaths { get; set; } = [.. ProtocolRoutePaths.CorsPaths.Select(x => new PathString(x.EnsureLeadingSlash()))];
}
