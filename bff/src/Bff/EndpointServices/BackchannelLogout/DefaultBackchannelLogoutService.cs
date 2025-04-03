// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.Bff.Internal;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// Default back-channel logout notification service implementation
/// </summary>
[Obsolete(Constants.ObsoleteMessages.ImplementationWillBeMadeInternal)]
public class DefaultBackchannelLogoutService(
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IOptionsMonitor<OpenIdConnectOptions> optionsMonitor,
    ISessionRevocationService userSession,
    ILogger<DefaultBackchannelLogoutService> logger) : IBackchannelLogoutService
{
    /// <summary>
    /// Authentication scheme provider
    /// </summary>
    protected readonly IAuthenticationSchemeProvider AuthenticationSchemeProvider = authenticationSchemeProvider;

    /// <summary>
    /// OpenID Connect options monitor
    /// </summary>
    protected readonly IOptionsMonitor<OpenIdConnectOptions> OptionsMonitor = optionsMonitor;

    /// <summary>
    /// Session revocation service
    /// </summary>
    protected readonly ISessionRevocationService UserSession = userSession;

    /// <summary>
    /// Logger
    /// </summary>
    protected readonly ILogger<DefaultBackchannelLogoutService> Logger = logger;


    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        Logger.LogDebug("Processing back-channel logout request");

        context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
        context.Response.Headers.Append("Pragma", "no-cache");

        try
        {
            if (context.Request.HasFormContentType)
            {
                var logoutToken = context.Request.Form[OidcConstants.BackChannelLogoutRequest.LogoutToken].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(logoutToken))
                {
                    var user = await ValidateLogoutTokenAsync(logoutToken);
                    if (user != null)
                    {
                        // these are the sub & sid to signout
                        var sub = user.FindFirst("sub")?.Value;
                        var sid = user.FindFirst("sid")?.Value;

                        Logger.BackChannelLogout(sub ?? "missing", sid ?? "missing");

                        await UserSession.RevokeSessionsAsync(new UserSessionsFilter
                        {
                            SubjectId = sub,
                            SessionId = sid
                        });

                        return;
                    }
                }
                else
                {
                    Logger.BackChannelLogoutError($"Failed to process backchannel logout request. 'Logout token is missing'");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.BackChannelLogoutError($"Failed to process backchannel logout request. '{ex.Message}'");
        }

        Logger.BackChannelLogoutError($"Failed to process backchannel logout request.");
        context.Response.StatusCode = 400;
    }

    /// <summary>
    /// Validates the logout token
    /// </summary>
    /// <param name="logoutToken"></param>
    /// <returns></returns>
    protected virtual async Task<ClaimsIdentity?> ValidateLogoutTokenAsync(string logoutToken)
    {
        var claims = await ValidateJwt(logoutToken);
        if (claims == null)
        {
            Logger.LogDebug("No claims in back-channel JWT");
            return null;
        }
        else
        {
            Logger.LogTrace("Claims found in back-channel JWT {claims}", claims.Claims);
        }

        if (claims.FindFirst("sub") == null && claims.FindFirst("sid") == null)
        {
            Logger.BackChannelLogoutError("Logout token missing sub and sid claims.");
            return null;
        }

        var nonce = claims.FindFirst("nonce")?.Value;
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            Logger.BackChannelLogoutError("Logout token should not contain nonce claim.");
            return null;
        }

        var eventsJson = claims.FindFirst("events")?.Value;
        if (string.IsNullOrWhiteSpace(eventsJson))
        {
            Logger.BackChannelLogoutError("Logout token missing events claim.");
            return null;
        }

        try
        {
            var events = JsonDocument.Parse(eventsJson);
            if (!events.RootElement.TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out _))
            {
                Logger.BackChannelLogoutError("Logout token contains missing http://schemas.openid.net/event/backchannel-logout value.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.BackChannelLogoutError($"Logout token contains invalid JSON in events claim value. '{ex.Message}'");
            return null;
        }

        return claims;
    }

    /// <summary>
    /// Validates and parses the logout token JWT 
    /// </summary>
    /// <param name="jwt"></param>
    /// <returns></returns>
    protected virtual async Task<ClaimsIdentity?> ValidateJwt(string jwt)
    {
        var handler = new JsonWebTokenHandler();
        var parameters = await GetTokenValidationParameters();

        var result = await handler.ValidateTokenAsync(jwt, parameters);
        if (result.IsValid)
        {
            Logger.LogDebug("Back-channel JWT validation successful");
            return result.ClaimsIdentity;
        }

        Logger.BackChannelLogoutError($"Error validating logout token. '{result.Exception.ToString()}'");
        return null;
    }

    /// <summary>
    /// Creates the token validation parameters based on the OIDC configuration
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected virtual async Task<TokenValidationParameters> GetTokenValidationParameters()
    {
        var scheme = await AuthenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
        if (scheme == null)
        {
            throw new Exception("Failed to obtain default challenge scheme");
        }

        var options = OptionsMonitor.Get(scheme.Name);
        if (options == null)
        {
            throw new Exception("Failed to obtain OIDC options for default challenge scheme");
        }

        var config = options.Configuration;
        if (config == null)
        {
            config = await options.ConfigurationManager?.GetConfigurationAsync(CancellationToken.None)!;
        }

        if (config == null)
        {
            throw new Exception("Failed to obtain OIDC configuration");
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = config.Issuer,
            ValidAudience = options.ClientId,
            IssuerSigningKeys = config.SigningKeys,

            NameClaimType = JwtClaimTypes.Name,
            RoleClaimType = JwtClaimTypes.Role
        };

        return parameters;
    }
}
