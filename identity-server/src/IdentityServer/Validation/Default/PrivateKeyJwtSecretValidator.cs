// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates a secret based on RS256 signed JWT token
/// </summary>
public class PrivateKeyJwtSecretValidator : ISecretValidator
{
    private readonly IIssuerNameService _issuerNameService;
    private readonly IReplayCache _replayCache;
    private readonly IServerUrls _urls;
    private readonly IdentityServerOptions _options;
    private readonly ILogger _logger;

    private const string Purpose = nameof(PrivateKeyJwtSecretValidator);

    /// <summary>
    /// Instantiates an instance of private_key_jwt secret validator
    /// </summary>
    public PrivateKeyJwtSecretValidator(
        IIssuerNameService issuerNameService,
        IReplayCache replayCache,
        IServerUrls urls,
        IdentityServerOptions options,
        ILogger<PrivateKeyJwtSecretValidator> logger)
    {
        _issuerNameService = issuerNameService;
        _replayCache = replayCache;
        _urls = urls;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Validates a secret
    /// </summary>
    /// <param name="secrets">The stored secrets.</param>
    /// <param name="parsedSecret">The received secret.</param>
    /// <returns>
    /// A validation result
    /// </returns>
    /// <exception cref="System.ArgumentException">ParsedSecret.Credential is not a JWT token</exception>
    public async Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret)
    {
        var fail = new SecretValidationResult { Success = false };
        var success = new SecretValidationResult { Success = true };

        if (parsedSecret.Type != ParsedSecretTypes.JwtBearer)
        {
            return fail;
        }

        if (!(parsedSecret.Credential is string jwtTokenString))
        {
            _logger.LogError("ParsedSecret.Credential is not a string.");
            return fail;
        }

        List<SecurityKey> trustedKeys;
        try
        {
            trustedKeys = await secrets.GetKeysAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not parse secrets");
            return fail;
        }

        if (!trustedKeys.Any())
        {
            _logger.LogError("There are no keys available to validate client assertion.");
            return fail;
        }

        var validAudiences = new[]
        {
            // token endpoint URL
            string.Concat(_urls.BaseUrl.EnsureTrailingSlash(), ProtocolRoutePaths.Token),
            // issuer URL + token (legacy support)
            string.Concat((await _issuerNameService.GetCurrentAsync()).EnsureTrailingSlash(), ProtocolRoutePaths.Token),
            // issuer URL
            await _issuerNameService.GetCurrentAsync(),
            // CIBA endpoint: https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html#auth_request
            string.Concat(_urls.BaseUrl.EnsureTrailingSlash(), ProtocolRoutePaths.BackchannelAuthentication),
            // PAR endpoint: https://datatracker.ietf.org/doc/html/rfc9126#name-request
            string.Concat(_urls.BaseUrl.EnsureTrailingSlash(), ProtocolRoutePaths.PushedAuthorization),

        }.Distinct();

        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKeys = trustedKeys,
            ValidateIssuerSigningKey = true,

            ValidIssuer = parsedSecret.Id,
            ValidateIssuer = true,

            ValidAudiences = validAudiences,
            ValidateAudience = true,

            RequireSignedTokens = true,
            RequireExpirationTime = true,

            ClockSkew = TimeSpan.FromMinutes(5)
        };

        var handler = new JsonWebTokenHandler() { MaximumTokenSizeInBytes = _options.InputLengthRestrictions.Jwt };
        var result = await handler.ValidateTokenAsync(jwtTokenString, tokenValidationParameters);
        if (!result.IsValid)
        {
            _logger.LogError(result.Exception, "JWT token validation error");
            return fail;
        }

        var jwtToken = (JsonWebToken)result.SecurityToken;
        if (jwtToken.Subject != jwtToken.Issuer)
        {
            _logger.LogError("Both 'sub' and 'iss' in the client assertion token must have a value of client_id.");
            return fail;
        }

        var exp = jwtToken.ValidTo;
        if (exp == DateTime.MinValue)
        {
            _logger.LogError("exp is missing.");
            return fail;
        }

        var jti = jwtToken.Id;
        if (jti.IsMissing())
        {
            _logger.LogError("jti is missing.");
            return fail;
        }

        if (await _replayCache.ExistsAsync(Purpose, jti))
        {
            _logger.LogError("jti is found in replay cache. Possible replay attack.");
            return fail;
        }
        else
        {
            await _replayCache.AddAsync(Purpose, jti, exp.AddMinutes(5));
        }

        return success;
    }
}
