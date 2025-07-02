// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.Revocation;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Default back-channel logout notification service implementation
/// </summary>
internal class DefaultBackchannelLogoutEndpoint(
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IOptionsMonitor<OpenIdConnectOptions> optionsMonitor,
    BuildUserSessionPartitionKey partitionKeyBuilder,
    ISessionRevocationService userSession,
    ILogger<DefaultBackchannelLogoutEndpoint> logger) : IBackchannelLogoutEndpoint
{
    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        logger.ProcessingBackChannelLogoutRequest(LogLevel.Debug);

        context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
        context.Response.Headers.Append("Pragma", "no-cache");

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

                    logger.BackChannelLogout(LogLevel.Debug, sub ?? "missing", sid ?? "missing");

                    await userSession.RevokeSessionsAsync(new UserSessionsFilter
                    {
                        PartitionKey = partitionKeyBuilder(),
                        SubjectId = sub,
                        SessionId = sid
                    }, ct);

                    return;
                }
            }
            else
            {
                logger.FailedToProcessBackchannelLogoutRequestMissingToken(LogLevel.Information);
            }
        }

        logger.FailedToProcessBackchannelLogoutRequest(LogLevel.Information);
        context.Response.StatusCode = 400;
    }

    /// <summary>
    /// Validates the logout token
    /// </summary>
    /// <param name="logoutToken"></param>
    /// <returns></returns>
    private async Task<ClaimsIdentity?> ValidateLogoutTokenAsync(string logoutToken)
    {
        var claims = await ValidateJwt(logoutToken);
        if (claims == null)
        {
            logger.NoClaimsInBackChannelJwt(LogLevel.Debug);
            return null;
        }
        else
        {
            logger.ClaimsFoundInBackChannelJwt(LogLevel.Trace, string.Join(',', claims.Claims));
        }

        if (claims.FindFirst("sub") == null && claims.FindFirst("sid") == null)
        {
            logger.LogoutTokenMissingSubAndSidClaims(LogLevel.Information);
            return null;
        }

        var nonce = claims.FindFirst("nonce")?.Value;
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            logger.LogoutTokenShouldNotContainNonceClaim(LogLevel.Information);
            return null;
        }

        var eventsJson = claims.FindFirst("events")?.Value;
        if (string.IsNullOrWhiteSpace(eventsJson))
        {
            logger.LogoutTokenMissingEventsClaim(LogLevel.Information);
            return null;
        }

        try
        {
            var events = JsonDocument.Parse(eventsJson);
            if (!events.RootElement.TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out _))
            {
                logger.LogoutTokenMissingBackchannelLogoutValue(LogLevel.Information);
                return null;
            }
        }
        catch (JsonException ex)
        {
            logger.LogoutTokenContainsInvalidJsonInEventsClaim(LogLevel.Information, ex);
            return null;
        }

        return claims;
    }

    /// <summary>
    /// Validates and parses the logout token JWT 
    /// </summary>
    /// <param name="jwt"></param>
    /// <returns></returns>
    private async Task<ClaimsIdentity?> ValidateJwt(string jwt)
    {
        var handler = new JsonWebTokenHandler();
        var parameters = await GetTokenValidationParameters();

        var result = await handler.ValidateTokenAsync(jwt, parameters);
        if (result.IsValid)
        {
            logger.BackChannelJwtValidationSuccessful(LogLevel.Debug);
            return result.ClaimsIdentity;
        }

        logger.ErrorValidatingLogoutToken(LogLevel.Information, result.Exception);
        return null;
    }

    /// <summary>
    /// Creates the token validation parameters based on the OIDC configuration
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task<TokenValidationParameters> GetTokenValidationParameters()
    {
        var scheme = await authenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
        if (scheme == null)
        {
            throw new InvalidOperationException("Failed to obtain default challenge scheme");
        }

        var options = optionsMonitor.Get(scheme.Name);
        if (options == null)
        {
            throw new InvalidOperationException("Failed to obtain OIDC options for default challenge scheme");
        }

        var config = options.Configuration;
        if (config == null)
        {
            config = await options.ConfigurationManager?.GetConfigurationAsync(CT.None)!;
        }

        if (config == null)
        {
            throw new InvalidOperationException("Failed to obtain OIDC configuration");
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
