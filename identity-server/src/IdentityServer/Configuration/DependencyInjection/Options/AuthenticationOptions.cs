// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Configures the login and logout views and behavior.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// Sets the cookie authentication scheme configured by the host used for interactive users. If not set, the scheme will be inferred from the host's default authentication scheme.
    /// This setting is typically used when AddPolicyScheme is used in the host as the default scheme.
    /// </summary>
    public string? CookieAuthenticationScheme { get; set; }

    /// <summary>
    /// Sets the cookie lifetime (only effective if the IdentityServer-provided cookie handler is used)
    /// </summary>
    public TimeSpan CookieLifetime { get; set; } = Constants.DefaultCookieTimeSpan;

    /// <summary>
    /// Specifies if the cookie should be sliding or not (only effective if the built-in cookie middleware is used)
    /// </summary>
    public bool CookieSlidingExpiration { get; set; }

    /// <summary>
    /// Specifies the SameSite mode for the internal authentication and temp cookie
    /// </summary>
    public SameSiteMode CookieSameSiteMode { get; set; } = SameSiteMode.None;

    /// <summary>
    /// Indicates if user must be authenticated to accept parameters to end session endpoint. Defaults to false.
    /// </summary>
    /// <value>
    /// <c>true</c> if required; otherwise, <c>false</c>.
    /// </value>
    public bool RequireAuthenticatedUserForSignOutMessage { get; set; }

    /// <summary>
    /// Gets or sets the name of the cookie used for the check session endpoint.
    /// </summary>
    public string CheckSessionCookieName { get; set; } = IdentityServerConstants.DefaultCheckSessionCookieName;

    /// <summary>
    /// Gets or sets the domain of the cookie used for the check session endpoint. Defaults to null.
    /// </summary>
    public string? CheckSessionCookieDomain { get; set; }

    /// <summary>
    /// Gets or sets the SameSite mode of the cookie used for the check session endpoint. Defaults to SameSiteMode.None.
    /// </summary>
    public SameSiteMode CheckSessionCookieSameSiteMode { get; set; } = SameSiteMode.None;

    /// <summary>
    /// If set, will require frame-src CSP headers being emitted on the end session callback endpoint which renders iframes to clients for front-channel sign out notification.
    /// </summary>
    public bool RequireCspFrameSrcForSignout { get; set; } = true;

    /// <summary>
    /// When enabled, all clients' token lifetimes (e.g. refresh tokens) will be tied to the user's session lifetime.
    /// This means when the user logs out, any revokable tokens will be removed.
    /// If using server-side sessions, expired sessions will also remove any revokable tokens, and backchannel logout will be triggered.
    /// An individual client can override this setting with its own CoordinateLifetimeWithUserSession configuration setting.
    /// </summary>
    public bool CoordinateClientLifetimesWithUserSession { get; set; }
}
