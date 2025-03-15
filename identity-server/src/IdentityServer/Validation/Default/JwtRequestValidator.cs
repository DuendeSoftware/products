// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates JWT authorization request objects
/// </summary>
public class JwtRequestValidator : IJwtRequestValidator
{
    private readonly string _audienceUri;

    /// <summary>
    /// JWT handler
    /// </summary>
    protected JsonWebTokenHandler Handler;

    /// <summary>
    /// The audience URI to use
    /// </summary>
    protected async Task<string> GetAudienceUri()
    {
        if (_audienceUri.IsPresent())
        {
            return _audienceUri;
        }

        return await IssuerNameService.GetCurrentAsync();
    }

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The issuer name service
    /// </summary>
    protected readonly IIssuerNameService IssuerNameService;

    /// <summary>
    /// Instantiates an instance of private_key_jwt secret validator
    /// </summary>
    public JwtRequestValidator(IdentityServerOptions options, IIssuerNameService issuerNameService,
        ILogger<JwtRequestValidator> logger)
    {
        Options = options;
        IssuerNameService = issuerNameService;
        Logger = logger;

        Handler = new JsonWebTokenHandler
        {
            MaximumTokenSizeInBytes = options.InputLengthRestrictions.Jwt
        };
    }

    /// <summary>
    /// Instantiates an instance of private_key_jwt secret validator (used for testing)
    /// </summary>
    internal JwtRequestValidator(string audience, ILogger<JwtRequestValidator> logger)
    {
        _audienceUri = audience;

        Logger = logger;
        Handler = new JsonWebTokenHandler();
    }

    /// <inheritdoc/>
    public virtual async Task<JwtRequestValidationResult> ValidateAsync(JwtRequestValidationContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("JwtRequestValidator.Validate");

        ArgumentNullException.ThrowIfNull(context);
        if (context.Client == null) throw new ArgumentNullException(nameof(context.Client));
        if (string.IsNullOrWhiteSpace(context.JwtTokenString)) throw new ArgumentNullException(nameof(context.JwtTokenString));

        var fail = new JwtRequestValidationResult { IsError = true };

        List<SecurityKey> trustedKeys;
        try
        {
            trustedKeys = await GetKeysAsync(context.Client);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Could not parse client secrets");
            return fail;
        }

        if (!trustedKeys.Any())
        {
            Logger.LogError("There are no keys available to validate JWT.");
            return fail;
        }

        JsonWebToken jwtSecurityToken;
        try
        {
            jwtSecurityToken = await ValidateJwtAsync(context, trustedKeys);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "JWT token validation error");
            return fail;
        }

        if (jwtSecurityToken.TryGetPayloadValue<string>(OidcConstants.AuthorizeRequest.Request, out _) ||
            jwtSecurityToken.TryGetPayloadValue<string>(OidcConstants.AuthorizeRequest.RequestUri, out _))
        {
            Logger.LogError("JWT payload must not contain request or request_uri");
            return fail;
        }

        var payload = await ProcessPayloadAsync(context, jwtSecurityToken);

        var result = new JwtRequestValidationResult
        {
            IsError = false,
            Payload = payload
        };

        Logger.LogDebug("JWT request object validation success.");
        return result;
    }

    /// <summary>
    /// Retrieves keys for a given client
    /// </summary>
    /// <param name="client">The client</param>
    /// <returns></returns>
    protected virtual Task<List<SecurityKey>> GetKeysAsync(Client client)
    {
        return client.ClientSecrets.GetKeysAsync();
    }

    /// <summary>
    /// Validates the JWT token
    /// </summary>
    protected virtual async Task<JsonWebToken> ValidateJwtAsync(JwtRequestValidationContext context, IEnumerable<SecurityKey> keys)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKeys = keys,
            ValidateIssuerSigningKey = true,

            ValidIssuer = context.Client.ClientId,
            ValidateIssuer = true,

            ValidAudience = await GetAudienceUri(),
            ValidateAudience = true,

            RequireSignedTokens = true,
            RequireExpirationTime = true,

            ClockSkew = Options.JwtValidationClockSkew
        };

        var strictJarValidation = context.StrictJarValidation.HasValue ? context.StrictJarValidation.Value : Options.StrictJarValidation;
        if (strictJarValidation)
        {
            tokenValidationParameters.ValidTypes = new[] { JwtClaimTypes.JwtTypes.AuthorizationRequest };
        }

        var result = await Handler.ValidateTokenAsync(context.JwtTokenString, tokenValidationParameters);
        if (!result.IsValid)
        {
            throw result.Exception;
        }

        return (JsonWebToken)result.SecurityToken;
    }

    /// <summary>
    /// Processes the JWT contents
    /// </summary>
    /// <param name="context"></param>
    /// <param name="token">The JWT token</param>
    /// <returns></returns>
    protected virtual Task<List<Claim>> ProcessPayloadAsync(JwtRequestValidationContext context, JsonWebToken token)
    {
        // filter JWT validation values
        var filter = Constants.Filters.JwtRequestClaimTypesFilter.ToList();
        if (context.IncludeJti)
        {
            // don't filter out the jti claim
            filter.Remove(JwtClaimTypes.JwtId);
        }

        var filtered = token.Claims.Where(claim => !filter.Contains(claim.Type));
        return Task.FromResult(filtered.ToList());
    }
}
