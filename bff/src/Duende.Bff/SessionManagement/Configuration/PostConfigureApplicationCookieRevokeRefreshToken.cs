// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace Duende.Bff;

/// <summary>
/// Cookie configuration to revoke refresh token on logout.
/// </summary>
public class PostConfigureApplicationCookieRevokeRefreshToken : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options;
    private readonly string? _scheme;
    private readonly ILogger<PostConfigureApplicationCookieRevokeRefreshToken> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="bffOptions"></param>
    /// <param name="authOptions"></param>
    /// <param name="logger"></param>
    public PostConfigureApplicationCookieRevokeRefreshToken(IOptions<BffOptions> bffOptions, IOptions<AuthenticationOptions> authOptions, ILogger<PostConfigureApplicationCookieRevokeRefreshToken> logger)
    {
        _options = bffOptions.Value;
        _scheme = authOptions.Value.DefaultAuthenticateScheme ?? authOptions.Value.DefaultScheme;
        _logger = logger;
    }

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
            _logger.LogDebug("Revoking user's refresh tokens in OnSigningOut for subject id: {subjectId}", ctx.HttpContext.User.FindFirst(JwtClaimTypes.Subject)?.Value);
            await ctx.HttpContext.RevokeRefreshTokenAsync();
            if (inner != null)
            {
                await inner.Invoke(ctx);
            }
        };

        return Callback;
    }
}