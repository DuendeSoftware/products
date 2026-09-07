// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for signin/out using the IdentityServer authentication scheme.
/// </summary>
public static class AuthenticationManagerExtensions
{
    extension(HttpContext context)
    {
        /// <summary>
        /// Signs the user in.
        /// </summary>
        /// <param name="user">The IdentityServer user.</param>
        /// <returns></returns>
        public async Task SignInAsync(IdentityServerUser user) => await context.SignInAsync(await context.GetCookieAuthenticationSchemeAsync(), user.CreatePrincipal());

        /// <summary>
        /// Signs the user in.
        /// </summary>
        /// <param name="user">The IdentityServer user.</param>
        /// <param name="properties">The authentication properties.</param>
        /// <returns></returns>
        public async Task SignInAsync(IdentityServerUser user, AuthenticationProperties properties) => await context.SignInAsync(await context.GetCookieAuthenticationSchemeAsync(), user.CreatePrincipal(), properties);

        internal async Task<string> GetCookieAuthenticationSchemeAsync()
        {
            var options = context.RequestServices.GetRequiredService<IdentityServerOptions>();
            if (options.Authentication.CookieAuthenticationScheme != null)
            {
                return options.Authentication.CookieAuthenticationScheme;
            }

            var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
            var scheme = await schemes.GetDefaultAuthenticateSchemeAsync();
            if (scheme == null)
            {
                throw new InvalidOperationException("No DefaultAuthenticateScheme found or no CookieAuthenticationScheme configured on IdentityServerOptions.");
            }

            return scheme.Name;
        }
    }
}
