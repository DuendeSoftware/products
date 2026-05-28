// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// CORS policy settings for IdentityServer's protocol endpoints. The underlying CORS
/// implementation is provided by ASP.NET Core and is automatically registered in the
/// dependency injection system.
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// Gets or sets the name of the ASP.NET Core CORS policy evaluated for requests to IdentityServer endpoints.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>"IdentityServer"</c>. The policy is implemented in terms of the
    /// <c>ICorsPolicyService</c> registered in the DI container. To customize which origins are
    /// allowed, provide a custom implementation of <c>ICorsPolicyService</c> rather than
    /// replacing this policy name.
    /// </remarks>
    public string CorsPolicyName { get; set; } = Constants.IdentityServerName;

    /// <summary>
    /// Gets or sets the value used for the <c>Access-Control-Max-Age</c> response header on CORS preflight
    /// requests.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>null</c>, which means no <c>Access-Control-Max-Age</c> header is emitted
    /// and browsers use their own default preflight cache duration.
    /// </remarks>
    public TimeSpan? PreflightCacheDuration { get; set; }

    /// <summary>
    /// Gets or sets the IdentityServer endpoint paths for which CORS is supported.
    /// </summary>
    /// <remarks>
    /// Defaults to the discovery, user info, token, and revocation endpoints.
    /// </remarks>
    public ICollection<PathString> CorsPaths { get; set; } = ProtocolRoutePaths.CorsPaths.Select(x => new PathString(x.EnsureLeadingSlash())).ToList();
}
