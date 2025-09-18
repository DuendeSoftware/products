// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.Configuration;

internal sealed record CookieConfiguration
{
    /// <summary>
    /// Indicates whether a cookie is inaccessible by client-side script. 
    /// </summary>
    public bool? HttpOnly { get; init; }

    /// <summary>
    /// The SameSite attribute of the cookie. 
    /// </summary>
    public SameSiteMode? SameSite { get; init; }

    /// <summary>
    /// The policy that will be used to determine <see cref="CookieOptions.Secure"/>.
    /// </summary>
    public CookieSecurePolicy? SecurePolicy { get; init; }

    /// <summary>
    /// The name of the cookie.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets the max-age for the cookie.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// The cookie path.
    /// </summary>
    /// <remarks>
    /// Determines the value that will be set for <see cref="CookieOptions.Path"/>.
    /// </remarks>
    public string? Path { get; init; }

    /// <summary>
    /// The domain to associate the cookie with.
    /// </summary>
    /// <remarks>
    /// Determines the value that will be set for <see cref="CookieOptions.Domain"/>.
    /// </remarks>
    public string? Domain { get; init; }

    internal void ApplyTo(CookieAuthenticationOptions options)
    {
        if (HttpOnly != null)
        {
            options.Cookie.HttpOnly = HttpOnly.Value;
        }
        if (SameSite != null)
        {
            options.Cookie.SameSite = SameSite.Value;
        }
        if (SecurePolicy != null)
        {
            options.Cookie.SecurePolicy = SecurePolicy.Value;
        }

        if (Name != null)
        {
            options.Cookie.Name = Name;
        }
        if (MaxAge != null)
        {
            options.Cookie.MaxAge = MaxAge.Value;
        }
        if (Path != null)
        {
            options.Cookie.Path = Path;
        }
        if (Domain != null)
        {
            options.Cookie.Domain = Domain;
        }
    }
}
