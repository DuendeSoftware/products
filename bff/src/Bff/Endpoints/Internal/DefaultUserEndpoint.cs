// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Service for handling user requests
/// </summary>
internal class DefaultUserEndpoint(IOptions<BffOptions> options, ILogger<DefaultUserEndpoint> logger) : IUserEndpoint
{
    /// <summary>
    /// The options
    /// </summary>
    private readonly BffOptions _options = options.Value;


    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        logger.LogDebug("Processing user request");

        context.CheckForBffMiddleware(_options);

        var result = await context.AuthenticateAsync();

        if (!result.Succeeded)
        {
            if (_options.AnonymousSessionResponse == AnonymousSessionResponse.Response200)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("null", Encoding.UTF8, ct);
            }
            else
            {
                context.Response.StatusCode = 401;
            }

            logger.LogDebug("User endpoint indicates the user is not logged in, using status code {code}", context.Response.StatusCode);
        }
        else
        {
            var claims = new List<ClaimRecord>();
            claims.AddRange(await GetUserClaimsAsync(result, ct));
            claims.AddRange(await GetManagementClaimsAsync(context, result, ct));

            var json = JsonSerializer.Serialize(claims);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, Encoding.UTF8, ct);

            logger.LogTrace("User endpoint indicates the user is logged in with claims {claims}", claims);
        }
    }

    /// <summary>
    /// Collect user-centric claims
    /// </summary>
    /// <returns></returns>
    private Task<IEnumerable<ClaimRecord>> GetUserClaimsAsync(AuthenticateResult authenticateResult, CT ct = default) =>
        Task.FromResult(authenticateResult.Principal?.Claims.Select(x => new ClaimRecord(x.Type, x.Value)) ?? Enumerable.Empty<ClaimRecord>());

    /// <summary>
    /// Collect management claims
    /// </summary>
    /// <returns></returns>
    private Task<IEnumerable<ClaimRecord>> GetManagementClaimsAsync(
        HttpContext context,
        AuthenticateResult authenticateResult,
        CT ct = default)
    {
        var claims = new List<ClaimRecord>();

        if (authenticateResult.Principal?.HasClaim(x => x.Type == Constants.ClaimTypes.LogoutUrl) != true)
        {
            var sessionId = authenticateResult.Principal?.FindFirst(JwtClaimTypes.SessionId)?.Value;
            claims.Add(new ClaimRecord(
                Constants.ClaimTypes.LogoutUrl,
                LogoutUrlBuilder.Build(context.Request.PathBase, _options, sessionId)));
        }

        if (authenticateResult.Properties != null)
        {
            if (authenticateResult.Properties.ExpiresUtc.HasValue)
            {
                var expiresInSeconds =
                    authenticateResult.Properties.ExpiresUtc.Value.Subtract(DateTimeOffset.UtcNow).TotalSeconds;
                claims.Add(new ClaimRecord(
                    Constants.ClaimTypes.SessionExpiresIn,
                    Math.Round(expiresInSeconds)));
            }

            if (authenticateResult.Properties.Items.TryGetValue(OpenIdConnectSessionProperties.SessionState, out var sessionState) && sessionState is not null)
            {
                claims.Add(new ClaimRecord(Constants.ClaimTypes.SessionState, sessionState));
            }
        }

        return Task.FromResult((IEnumerable<ClaimRecord>)claims);
    }

}
