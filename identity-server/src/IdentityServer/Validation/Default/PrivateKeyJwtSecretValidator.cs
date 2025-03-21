// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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

        // Decide whether to enforce strict audience validation or not.
        var enforceStrictAud = _options.Preview.StrictClientAssertionAudienceValidation;

        try
        {
            // Read the token so we can get the "typ" header value if it exists.
            var handlerForHeader = new JsonWebTokenHandler();
            var tokenForHeader = handlerForHeader.ReadJsonWebToken(jwtTokenString);
            var jwtTyp = tokenForHeader.GetHeaderValue<string>("typ");

            // If strict mode is not enabled by option but the "typ" header value "client-authentication+jwt" is provided,
            // enforce strict audience validation.
            if (string.Equals(jwtTyp, "client-authentication+jwt", StringComparison.OrdinalIgnoreCase))
            {
                enforceStrictAud = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading JWT header.");
            return fail;
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKeys = trustedKeys,
            ValidateIssuerSigningKey = true,

            ValidIssuer = parsedSecret.Id,
            ValidateIssuer = true,

            RequireSignedTokens = true,
            RequireExpirationTime = true,

            ClockSkew = _options.JwtValidationClockSkew
        };

        var issuer = await _issuerNameService.GetCurrentAsync();

        if (enforceStrictAud)
        {
            // New strict audience validation requires that the audience be the issuer identifier, disallows multiple
            // audiences in an array, and even disallows wrapping even a single audience in an array 
            tokenValidationParameters.AudienceValidator = (audiences, token, parameters) =>
            {
                // There isn't a particularly nice way to distinguish between a claim that is a single string wrapped in
                // an array and just a single string when using a JsonWebToken. The jwt.GetClaim function and jwt.Claims
                // collection both convert that into a string valued claim. However, GetPayloadValue<object> does not do
                // any type inferencing, so we can call that, and then check if the result is actually a string
                var audValue = ((JsonWebToken)token).GetPayloadValue<object>("aud");
                return audValue is string audString &&
                       AudiencesMatch(audString, issuer);
            };

            // Strict audience validation requires that the token type be "client-authentication+jwt"
            tokenValidationParameters.ValidTypes = ["client-authentication+jwt"];
        }
        else
        {
            // Legacy behavior with a set of allowed audiences.
            tokenValidationParameters.ValidateAudience = true;
            tokenValidationParameters.ValidAudiences = new[]
            {
                // token endpoint URL
                string.Concat(_urls.BaseUrl.EnsureTrailingSlash(), ProtocolRoutePaths.Token),
                // issuer URL + token (legacy support)
                string.Concat((await _issuerNameService.GetCurrentAsync()).EnsureTrailingSlash(), ProtocolRoutePaths.Token),
                // issuer URL
                issuer,
                // CIBA endpoint: https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html#auth_request
                string.Concat(_urls.BaseUrl.EnsureTrailingSlash(), ProtocolRoutePaths.BackchannelAuthentication),
                // PAR endpoint: https://datatracker.ietf.org/doc/html/rfc9126#name-request
                string.Concat(_urls.BaseUrl.EnsureTrailingSlash(), ProtocolRoutePaths.PushedAuthorization),

            }.Distinct();
        }

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

    // AudiencesMatch and AudiencesMatchIgnoringTrailingSlash are based on code from 
    // https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/bef98ca10ae55603ce6d37dfb7cd5af27791527c/src/Microsoft.IdentityModel.Tokens/Validators.cs#L158-L193
    private bool AudiencesMatch(string tokenAudience, string validAudience)
    {
        if (validAudience.Length == tokenAudience.Length)
        {
            if (string.Equals(validAudience, tokenAudience))
            {
                return true;
            }
        }

        return AudiencesMatchIgnoringTrailingSlash(tokenAudience, validAudience);
    }

    private bool AudiencesMatchIgnoringTrailingSlash(string tokenAudience, string validAudience)
    {
        var length = -1;

        if (validAudience.Length == tokenAudience.Length + 1 &&
            validAudience.EndsWith('/'))
        {
            length = validAudience.Length - 1;
        }
        else if (tokenAudience.Length == validAudience.Length + 1 &&
                 tokenAudience.EndsWith('/'))
        {
            length = tokenAudience.Length - 1;
        }

        // the length of the audiences is different by more than 1 and neither ends in a "/"
        if (length == -1)
        {
            return false;
        }

        if (string.CompareOrdinal(validAudience, 0, tokenAudience, 0, length) == 0)
        {
            _logger.LogInformation("Audience Validated.Audience: '{audience}'", tokenAudience);

            return true;
        }

        return false;
    }
}
