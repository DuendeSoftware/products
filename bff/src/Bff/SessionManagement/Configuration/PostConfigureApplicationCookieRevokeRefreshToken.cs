// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Cookie configuration to revoke refresh token on logout.
/// </summary>
[Obsolete(Constants.ObsoleteMessages.ImplementationWillBeMadeInternal)]
public class PostConfigureApplicationCookieRevokeRefreshToken(
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
            logger.LogDebug("Revoking user's refresh tokens in OnSigningOut for subject id: {subjectId}", ctx.HttpContext.User.FindFirst(JwtClaimTypes.Subject)?.Value);
            await ctx.HttpContext.RevokeRefreshTokenAsync();

            await inner.Invoke(ctx);
        }

        return Callback;
    }
}
