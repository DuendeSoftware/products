// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for Pushed Authorization Requests (PAR), which allow clients to push authorization
/// parameters to IdentityServer before initiating the authorization flow.
/// </summary>
public class PushedAuthorizationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether all clients are required to use Pushed Authorization Requests globally. When enabled, the
    /// authorize endpoint will reject requests that were not previously pushed via the PAR
    /// endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Individual clients can also require PAR via their own
    /// <c>RequirePushedAuthorization</c> configuration flag; PAR is required for a client if
    /// either this global flag or the per-client flag is set.
    /// </remarks>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of pushed authorization requests, in seconds.
    /// </summary>
    /// <remarks>
    /// Defaults to 600 seconds (10 minutes). The lifetime begins when the PAR endpoint receives
    /// the request and must cover the entire interactive login flow, including user interaction
    /// such as entering credentials and granting consent. Setting this too low will cause login
    /// failures for interactive users. Security profiles such as FAPI 2.0 recommend a maximum
    /// of 10 minutes to limit the window for pre-generated request attacks. A per-client
    /// configuration setting takes precedence over this global value.
    /// </remarks>
    public int Lifetime { get; set; } = 60 * 10;

    /// <summary>
    /// Gets or sets a value indicating whether clients may use redirect URIs in pushed authorization requests that were not
    /// previously registered.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Enable with caution; allowing unregistered redirect URIs
    /// reduces the protection that pre-registration provides against open redirect attacks.
    /// </remarks>
    public bool AllowUnregisteredPushedRedirectUris { get; set; }
}
