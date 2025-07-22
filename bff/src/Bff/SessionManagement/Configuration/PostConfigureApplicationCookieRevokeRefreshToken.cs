// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Duende.Bff.Otel;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration to revoke refresh token on logout.
/// </summary>
internal class PostConfigureApplicationCookieRevokeRefreshToken(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    IOptions<BffOptions> bffOptions,
    ILogger<PostConfigureApplicationCookieRevokeRefreshToken> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options = bffOptions.Value;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return;
        }

        if (_options.RevokeRefreshTokenOnLogout)
        {
            options.Events.OnSigningOut = CreateCallback(options.Events.OnSigningOut);
        }
    }

    private Func<CookieSigningOutContext, Task> CreateCallback(Func<CookieSigningOutContext, Task> inner)
    {
        async Task Callback(CookieSigningOutContext ctx)
        {
            // As of this time of writing, we don't have an extension point to revoke refresh tokens.
            // Should that become needed, then we should implement an ITokenRevocationService
            logger.RevokingUserRefreshTokensOnSigningOut(LogLevel.Debug, ctx.HttpContext.User.FindFirst(JwtClaimTypes.Subject)?.Value);
            await ctx.HttpContext.RevokeRefreshTokenAsync(ct: ctx.HttpContext.RequestAborted);

            await inner.Invoke(ctx);
        }

        return Callback;
    }
}
