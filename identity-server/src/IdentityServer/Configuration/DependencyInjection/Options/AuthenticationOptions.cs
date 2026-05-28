// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Configures login, logout, and cookie behavior for interactive users.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// Gets or sets the cookie authentication scheme used for interactive users. When not set, the scheme is
    /// inferred from the host's default authentication scheme.
    /// </summary>
    /// <remarks>
    /// This setting is typically needed when <c>AddPolicyScheme</c> is used as the default
    /// authentication scheme in the host application, so that IdentityServer can resolve the
    /// correct underlying cookie scheme.
    /// </remarks>
    public string? CookieAuthenticationScheme { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of the authentication cookie. Only effective when the IdentityServer-provided
    /// cookie handler is used.
    /// </summary>
    /// <remarks>
    /// Defaults to 10 hours (<see cref="Constants.DefaultCookieTimeSpan"/>).
    /// </remarks>
    public TimeSpan CookieLifetime { get; set; } = Constants.DefaultCookieTimeSpan;

    /// <summary>
    /// Gets or sets a value indicating whether the authentication cookie uses sliding expiration. Only effective when
    /// the IdentityServer-provided cookie handler is used.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When <c>true</c>, the cookie expiration is reset on each
    /// authenticated request, keeping active users logged in.
    /// </remarks>
    public bool CookieSlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the <c>SameSite</c> mode applied to internal authentication and temporary cookies.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SameSiteMode.None"/>, which is required for cross-site scenarios
    /// such as iframes used by the check-session endpoint.
    /// </remarks>
    public SameSiteMode CookieSameSiteMode { get; set; } = SameSiteMode.None;

    /// <summary>
    /// Gets or sets a value indicating whether the user must be authenticated before IdentityServer will accept sign-out
    /// parameters on the end-session endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When <c>true</c>, unauthenticated requests to the end-session
    /// endpoint will not process logout parameters such as <c>id_token_hint</c>.
    /// </remarks>
    public bool RequireAuthenticatedUserForSignOutMessage { get; set; }

    /// <summary>
    /// Gets or sets the name of the cookie used by the check-session endpoint to track the user's session state.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="IdentityServerConstants.DefaultCheckSessionCookieName"/>
    /// (<c>"idsrv.session"</c>).
    /// </remarks>
    public string CheckSessionCookieName { get; set; } = IdentityServerConstants.DefaultCheckSessionCookieName;

    /// <summary>
    /// Gets or sets the domain of the cookie used by the check-session endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>null</c>, which means the cookie is scoped to the current host.
    /// </remarks>
    public string? CheckSessionCookieDomain { get; set; }

    /// <summary>
    /// Gets or sets the <c>SameSite</c> mode of the cookie used by the check-session endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SameSiteMode.None"/> to support cross-origin iframe-based
    /// session monitoring.
    /// </remarks>
    public SameSiteMode CheckSessionCookieSameSiteMode { get; set; } = SameSiteMode.None;

    /// <summary>
    /// Gets or sets a value indicating whether Content Security Policy headers on the end-session endpoint are enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. When enabled, the end-session endpoint emits CSP headers
    /// including <c>default-src 'none'</c>, a <c>style-src</c> with the expected style hash,
    /// and additional fetch directives. Despite the property name referencing <c>frame-src</c>,
    /// the full set of CSP fetch directives is applied.
    /// </remarks>
    public bool RequireCspFrameSrcForSignout { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether all clients' token lifetimes are tied to the user's session lifetime at IdentityServer.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When enabled, logging out revokes all revocable tokens
    /// (e.g., refresh tokens) for the user. When server-side sessions are also used, expired
    /// sessions trigger token revocation and back-channel logout. Individual clients can override
    /// this behavior via their own <c>CoordinateLifetimeWithUserSession</c> setting.
    /// </remarks>
    public bool CoordinateClientLifetimesWithUserSession { get; set; }
}
