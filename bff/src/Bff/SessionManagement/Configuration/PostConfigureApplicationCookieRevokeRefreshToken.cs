// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Configuration;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration to revoke refresh token on logout.
/// </summary>
internal class PostConfigureApplicationCookieRevokeRefreshToken(
    IOptions<BffOptions> bffOptions,
    IOptions<AuthenticationOptions> authOptions,
    ILogger<PostConfigureApplicationCookieRevokeRefreshToken> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options = bffOptions.Value;
    private readonly string? _scheme = authOptions.Value.DefaultAuthenticateScheme ?? authOptions.Value.DefaultScheme;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (_options.RevokeRefreshTokenOnLogout && name == _scheme)
        {
            options.Events.OnSigningOut = CreateCallback(options.Events.OnSigningOut);
        }
    }

    private Func<CookieSigningOutContext, Task> CreateCallback(Func<CookieSigningOutContext, Task> inner)
    {
        async Task Callback(CookieSigningOutContext ctx)
        {
            // Todo: Ev: logging with sourcegens
            // todo: ev: should we have userparameters here?
            logger.LogDebug("Revoking user's refresh tokens in OnSigningOut for subject id: {subjectId}", ctx.HttpContext.User.FindFirst(JwtClaimTypes.Subject)?.Value);
            await ctx.HttpContext.RevokeRefreshTokenAsync(ct: ctx.HttpContext.RequestAborted);

            await inner.Invoke(ctx);
        }

        return Callback;
    }
}
