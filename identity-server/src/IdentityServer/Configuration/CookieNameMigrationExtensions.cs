// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Extension methods to support seamless cookie name migration.
/// </summary>
public static class CookieNameMigrationExtensions
{
    /// <summary>
    /// Adds middleware that transparently migrates requests using an old cookie name to a new
    /// cookie name. This enables seamless migration when cookie names change (e.g., when
    /// adopting the <c>__Host-</c> prefix in IdentityServer 8.0) without invalidating existing
    /// user sessions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Register this middleware <b>before</b> <c>app.UseIdentityServer()</c> in the pipeline.
    /// </para>
    /// <para>
    /// On each request where the old cookie is present but the new one is absent, the middleware:
    /// <list type="number">
    ///   <item>Patches the incoming request so downstream auth handlers find the value under the new name.</item>
    ///   <item>On the response: issues a <c>Set-Cookie</c> for the new name and expires the old one.</item>
    /// </list>
    /// </para>
    /// <para>
    /// This is a transient migration aid. Once all active sessions have been re-issued under the
    /// new cookie name the middleware can be removed.
    /// </para>
    /// <para>
    /// To migrate from the IdentityServer 7.x defaults to the 8.0 defaults, register twice:
    /// <code>
    /// app.MigrateIdentityServerCookieName("idsrv", "__Host-idsrv");
    /// app.MigrateIdentityServerCookieName("idsrv.external", "__Host-idsrv.external");
    /// app.UseIdentityServer();
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="oldCookieName">The old cookie name to migrate from.</param>
    /// <param name="newCookieName">The new cookie name to migrate to.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder MigrateIdentityServerCookieName(
        this IApplicationBuilder app,
        string oldCookieName,
        string newCookieName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldCookieName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newCookieName);

        return app.Use(async (context, next) =>
        {
            var oldValue = context.Request.Cookies[oldCookieName];
            var newValue = context.Request.Cookies[newCookieName];

            if (oldValue != null && newValue == null)
            {
                // Patch the request Cookie header so downstream auth handlers
                // find the encrypted value under the new cookie name.
                var existingHeader = context.Request.Headers.Cookie.ToString();
                var newCookiePair = $"{newCookieName}={oldValue}";
                context.Request.Headers.Cookie = string.IsNullOrWhiteSpace(existingHeader)
                    ? newCookiePair
                    : existingHeader + "; " + newCookiePair;

                // Once the response starts, re-issue the cookie under the new name
                // and expire the old one so the migration happens transparently.
                context.Response.OnStarting(() =>
                {
                    var isHostPrefixed = newCookieName.StartsWith("__Host-", StringComparison.Ordinal);
                    var idsrvOptions = context.RequestServices.GetRequiredService<IdentityServerOptions>();

                    context.Response.Cookies.Append(newCookieName, oldValue, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = isHostPrefixed || context.Request.IsHttps,
                        Path = "/",
                        IsEssential = true,
                        SameSite = idsrvOptions.Authentication.CookieSameSiteMode,
                        Domain = isHostPrefixed ? null : default
                    });

                    context.Response.Cookies.Delete(oldCookieName, new CookieOptions
                    {
                        Path = "/",
                        Domain = null
                    });

                    return Task.CompletedTask;
                });
            }

            await next(context);
        });
    }
}
