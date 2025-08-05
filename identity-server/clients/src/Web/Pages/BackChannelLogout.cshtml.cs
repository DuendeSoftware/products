// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace Web.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken(Order = 1001)]
public class BackChannelLogoutModel(LogoutSessionManager logoutManager, IConfiguration configuration, ILogger<BackChannelLogoutModel> logger) : PageModel
{
    public async Task<ActionResult> OnPostAsync(string logout_token)
    {
        try
        {
            Response.Headers.Append("Cache-Control", "no-cache, no-store");
            Response.Headers.Append("Pragma", "no-cache");

            var user = await ValidateLogoutToken(logout_token);

            var sub = user.FindFirst("sub")?.Value;
            var sid = user.FindFirst("sid")?.Value;

            logoutManager.Add(sub, sid);

            return Page();
        }
        catch (Exception)
        {
            logger.LogDebug("Failed to handle a logout token: {token}", logout_token);
            return BadRequest();
        }
    }
    private async Task<ClaimsPrincipal> ValidateLogoutToken(string logoutToken)
    {
        var claims = await ValidateJwt(logoutToken);

        if (claims.FindFirst("sub") == null && claims.FindFirst("sid") == null)
        {
            throw new Exception("Invalid logout token");
        }

        var nonce = claims.FindFirstValue("nonce");
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            throw new Exception("Invalid logout token");
        }

        var eventsJson = claims.FindFirst("events")?.Value;
        if (string.IsNullOrWhiteSpace(eventsJson))
        {
            throw new Exception("Invalid logout token");
        }

        var events = JsonDocument.Parse(eventsJson).RootElement;
        var logoutEvent = events.TryGetString("http://schemas.openid.net/event/backchannel-logout");
        if (logoutEvent == null)
        {
            throw new Exception("Invalid logout token");
        }

        return claims;
    }

    private async Task<ClaimsPrincipal> ValidateJwt(string jwt)
    {
        // read discovery document to find issuer and key material
        var client = new HttpClient();
        var disco = await client.GetDiscoveryDocumentAsync(configuration["is-host"]);

        var keys = new List<SecurityKey>();
        foreach (var webKey in disco.KeySet?.Keys ?? [])
        {
            var key = new JsonWebKey
            {
                Kty = webKey.Kty,
                Alg = webKey.Alg,
                Kid = webKey.Kid,
                X = webKey.X,
                Y = webKey.Y,
                Crv = webKey.Crv,
                E = webKey.E,
                N = webKey.N,
            };
            keys.Add(key);
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = disco.Issuer,
            ValidAudience = "web",
            IssuerSigningKeys = keys,
            NameClaimType = JwtClaimTypes.Name,
            RoleClaimType = JwtClaimTypes.Role
        };

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var user = handler.ValidateToken(jwt, parameters, out var _);
        return user;
    }
}
