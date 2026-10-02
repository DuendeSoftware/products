// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Validation;

internal partial class TokenValidator(
    IdentityServerOptions options,
    IIssuerNameService issuerNameService,
    IClientStore clients,
    IProfileService profile,
    IReferenceTokenStore referenceTokenStore,
    ICustomTokenValidator customValidator,
    IKeyMaterialService keys,
    ISessionCoordinationService sessionCoordinationService,
    TimeProvider timeProvider,
    ILogger<TokenValidator> logger)
    : ITokenValidator
{
    private readonly ILogger _logger = logger;
    private readonly TokenValidationLog _log = new();

    public async Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string? clientId, bool validateLifetime, Ct ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateIdentityToken");

        _logger.StartIdentityTokenValidation();

        if (token.Length > options.InputLengthRestrictions.Jwt)
        {
            _logger.JWTTooLong();
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        if (clientId.IsMissing())
        {
            clientId = GetClientIdFromJwt(token);

            if (clientId.IsMissing())
            {
                _logger.NoClientIdSuppliedCanTFindIdIn();
                return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
            }
        }

        _log.ClientId = clientId;
        _log.ValidateLifetime = validateLifetime;

        var client = await clients.FindEnabledClientByIdAsync(clientId, ct);
        if (client == null)
        {
            _logger.UnknownOrDisabledClient(clientId);
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        _log.ClientName = client.ClientName;
        _logger.ClientFoundForTokenValidation(client.ClientId, client.ClientName);

        var keys1 = await keys.GetValidationKeysAsync(ct);
        var result = await ValidateJwtAsync(token, keys1, ct, validateLifetime: validateLifetime, audience: clientId);

        result.Client = client;

        if (result.IsError)
        {
            LogInformation("Error validating JWT");
            return result;
        }

        _logger.CallingIntoCustomTokenValidator(customValidator.GetType().FullName);
        var customResult = await customValidator.ValidateIdentityTokenAsync(result, ct);

        if (customResult.IsError)
        {
            LogInformation("Custom validator failed: " + (customResult.Error ?? "unknown"));
            return customResult;
        }

        _log.Claims = customResult.Claims?.ToClaimsDictionary() ?? [];

        LogSuccess();
        return customResult;
    }

    public async Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string? expectedScope, Ct ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateAccessToken");

        _logger.StartAccessTokenValidation();

        _log.ExpectedScope = expectedScope;
        _log.ValidateLifetime = true;

        TokenValidationResult result;

        // Distinguish JWTs from reference tokens by looking for '.', but bound the scan.
        // Scanning the whole string would let an attacker send an arbitrarily long token with
        // no '.' and force a full scan before any length check could reject it.
        //
        // Scanning only the first maxLength characters is safe: if the token is longer than
        // that it exceeds *both* limits, so it is rejected below whichever way we classify it.
        // Within maxLength the window is the entire token, so classification is exact.
        var restrictions = options.InputLengthRestrictions;
        var maxLength = Math.Max(restrictions.Jwt, restrictions.TokenHandle);
        // Clamp to >= 0 so a misconfigured negative restriction cannot make AsSpan throw
        // before the length check below rejects the token.
        var scanLength = Math.Clamp(maxLength, 0, token.Length);
        var isJwt = token.AsSpan(0, scanLength).Contains('.');

        if (token.Length > (isJwt ? restrictions.Jwt : restrictions.TokenHandle))
        {
            // These two messages are the only externally observable difference between the
            // JWT and reference-token rejection paths (Error and ErrorDescription are
            // identical for both), so a test asserts on them to prove the scan above stays
            // within maxLength. Changing the wording will break that test.
            if (isJwt)
            {
                LogJwtTooLong();
            }
            else
            {
                LogTokenHandleTooLong();
            }

            return new TokenValidationResult
            {
                IsError = true,
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken,
                ErrorDescription = "Token too long"
            };
        }

        if (isJwt)
        {
            _log.AccessTokenType = nameof(AccessTokenType.Jwt);
            result = await ValidateJwtAsync(
                token,
                await keys.GetValidationKeysAsync(ct),
                ct);
        }
        else
        {
            _log.AccessTokenType = nameof(AccessTokenType.Reference);
            result = await ValidateReferenceAccessTokenAsync(token, ct);
        }

        var claimsDictionary = result.Claims?.ToClaimsDictionary() ?? [];
        _log.Claims = claimsDictionary;

        if (result.IsError)
        {
            return result;
        }

        // make sure client is still active (if client_id claim is present)
        var clientClaim = result.Claims?.FirstOrDefault(c => c.Type == JwtClaimTypes.ClientId);
        if (clientClaim != null)
        {
            var client = await clients.FindEnabledClientByIdAsync(clientClaim.Value, ct);
            if (client == null)
            {
                _logger.ClientDeletedOrDisabled(clientClaim.Value);

                result.IsError = true;
                result.Error = OidcConstants.ProtectedResourceErrors.InvalidToken;
                result.Claims = null;

                return result;
            }
        }

        // make sure user is still active (if sub claim is present)
        var subClaim = result.Claims?.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject);
        if (subClaim != null)
        {
            var principal = Principal.Create("tokenvalidator", result.Claims?.ToArray() ?? []);

            if (result.ReferenceTokenId.IsPresent())
            {
                principal.Identities.First()
                    .AddClaim(new Claim(JwtClaimTypes.ReferenceTokenId, result.ReferenceTokenId));
            }

            if (result.Client == null)
            {
                LogInformation("Access token has no associated client");
                return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
            }

            var resultClient = result.Client;
            var isActiveCtx = new IsActiveContext(principal, resultClient,
                IdentityServerConstants.ProfileIsActiveCallers.AccessTokenValidation);
            await profile.IsActiveAsync(isActiveCtx, ct);

            if (isActiveCtx.IsActive == false)
            {
                _logger.UserMarkedAsNotActive(subClaim.Value);

                result.IsError = true;
                result.Error = OidcConstants.ProtectedResourceErrors.InvalidToken;
                result.Claims = null;

                return result;
            }

            var sub = subClaim.Value;
            var sid = principal.FindFirstValue("sid");
            if (sid != null)
            {
                var sessionResult = await sessionCoordinationService.ValidateSessionAsync(new SessionValidationRequest
                {
                    SubjectId = sub,
                    SessionId = sid,
                    Client = resultClient,
                    Type = SessionValidationType.AccessToken
                }, ct);

                if (!sessionResult)
                {
                    _logger.ServerSideSessionInvalidForSubjectIdAnd(sub, sid);
                    return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
                }
            }
        }

        // check expected scope(s)
        if (expectedScope.IsPresent())
        {
            var scope = result.Claims?.FirstOrDefault(c =>
                c.Type == JwtClaimTypes.Scope && c.Value == expectedScope);
            if (scope == null)
            {
                LogInformation($"Checking for expected scope {expectedScope} failed");
                return Invalid(OidcConstants.ProtectedResourceErrors.InsufficientScope);
            }
        }

        _logger.CallingIntoCustomTokenValidatorTokenValidator(customValidator.GetType().FullName);
        var customResult = await customValidator.ValidateAccessTokenAsync(result, ct);

        if (customResult.IsError)
        {
            LogInformation("Custom validator failed: " + (customResult.Error ?? "unknown"));
            return customResult;
        }

        // add claims again after custom validation
        _log.Claims = customResult.Claims.ToClaimsDictionary();

        LogSuccess();
        return customResult;
    }

    private async Task<TokenValidationResult> ValidateJwtAsync(string jwtString,
        IEnumerable<SecurityKeyInfo> validationKeys, Ct ct, bool validateLifetime = true, string? audience = null)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateJwt");

        var handler = new JsonWebTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = await issuerNameService.GetCurrentAsync(ct),
            IssuerSigningKeys = validationKeys.Select(k => k.Key),
            ValidateLifetime = validateLifetime,
            ClockSkew = options.JwtValidationClockSkew
        };

        if (audience.IsPresent())
        {
            parameters.ValidAudience = audience;
        }
        else
        {
#pragma warning disable CA5404 // No audience is specified — issuer, signature, and lifetime are still validated; token type is checked as a compensating control
            parameters.ValidateAudience = false;
#pragma warning restore CA5404

            // if no audience is specified, we make at least sure that it is an access token
            if (options.AccessTokenJwtType.IsPresent())
            {
                parameters.ValidTypes = new[] { options.AccessTokenJwtType };
            }
        }

        var result = await handler.ValidateTokenAsync(jwtString, parameters);
        if (!result.IsValid)
        {
            if (result.Exception is SecurityTokenExpiredException expiredException)
            {
                _logger.JWTTokenValidationErrorTokenValidator(expiredException, expiredException.Message);
                return Invalid(OidcConstants.ProtectedResourceErrors.ExpiredToken);
            }
            else
            {
                _logger.JWTTokenValidationErrorTokenValidator2(result.Exception, result.Exception.Message);
                return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
            }
        }

        var id = result.ClaimsIdentity;

        // if access token contains an ID, log it
        var jwtId = id.FindFirst(JwtClaimTypes.JwtId);
        if (jwtId != null)
        {
            _log.JwtId = jwtId.Value;
        }

        // load the client that belongs to the client_id claim
        Client? client = null;
        var clientId = id.FindFirst(JwtClaimTypes.ClientId);
        if (clientId != null)
        {
            client = await clients.FindEnabledClientByIdAsync(clientId.Value, ct);
            if (client == null)
            {
                LogInformation($"Client deleted or disabled: {clientId}");
                return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
            }
        }

        var claims = id.Claims.ToList();

        // check the scope format (array vs space delimited string)
        var scopes = claims.Where(c => c.Type == JwtClaimTypes.Scope).ToArray();
        foreach (var scope in scopes)
        {
            if (scope.Value.Contains(' ', StringComparison.InvariantCulture))
            {
                claims.Remove(scope);
                var values = scope.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var value in values)
                {
                    claims.Add(new Claim(JwtClaimTypes.Scope, value));
                }
            }
        }

        return new TokenValidationResult
        {
            IsError = false,

            Claims = claims,
            Client = client,
            Jwt = jwtString
        };
    }

    private async Task<TokenValidationResult> ValidateReferenceAccessTokenAsync(string tokenHandle, Ct ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateReferenceAccessToken");

        _log.TokenHandle = tokenHandle;
        var token = await referenceTokenStore.GetReferenceTokenAsync(tokenHandle, ct);

        if (token == null)
        {
            LogInformation("Invalid reference token.");
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        if (token.CreationTime.HasExceeded(token.Lifetime, timeProvider.GetUtcNow().UtcDateTime))
        {
            LogInformation("Token expired.");

            await referenceTokenStore.RemoveReferenceTokenAsync(tokenHandle, ct);
            return Invalid(OidcConstants.ProtectedResourceErrors.ExpiredToken);
        }

        // load the client that is defined in the token
        Client? client = null;
        if (token.ClientId != null)
        {
            client = await clients.FindEnabledClientByIdAsync(token.ClientId, ct);
        }

        if (client == null)
        {
            LogInformation($"Client deleted or disabled: {token.ClientId}");
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        return new TokenValidationResult
        {
            IsError = false,

            Client = client,
            Claims = ReferenceTokenToClaims(token),
            ReferenceToken = token,
            ReferenceTokenId = tokenHandle
        };
    }

    private static List<Claim> ReferenceTokenToClaims(Token token)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Issuer, token.Issuer),
            new Claim(JwtClaimTypes.NotBefore,
                new DateTimeOffset(token.CreationTime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim(JwtClaimTypes.IssuedAt, new DateTimeOffset(token.CreationTime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new Claim(JwtClaimTypes.Expiration,
                new DateTimeOffset(token.CreationTime).AddSeconds(token.Lifetime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrEmpty(token.Confirmation))
        {
            claims.Add(new Claim(JwtClaimTypes.Confirmation, token.Confirmation, IdentityServerConstants.ClaimValueTypes.Json));
        }

        foreach (var aud in token.Audiences)
        {
            claims.Add(new Claim(JwtClaimTypes.Audience, aud));
        }

        claims.AddRange(token.Claims.Where(c =>
            c.Type != JwtClaimTypes.IssuedAt &&
            c.Type != JwtClaimTypes.Issuer &&
            c.Type != JwtClaimTypes.NotBefore &&
            c.Type != JwtClaimTypes.Expiration
        ));
        return claims;
    }

    private string? GetClientIdFromJwt(string token)
    {
        try
        {
            var jwt = new JwtSecurityToken(token);
            var clientId = jwt.Audiences.FirstOrDefault();

            return clientId;
        }
        catch (Exception ex)
        {
            _logger.MalformedJWTToken(ex, ex.Message);
            return null;
        }
    }

    private static TokenValidationResult Invalid(string error) => new TokenValidationResult
    {
        IsError = true,
        Error = error
    };

    private void LogInformation(string message) => _logger.LogMessageTokenValidator(message, _log);

    private void LogSuccess() => _logger.TokenValidationSuccess(_log);

    [LoggerMessage(Level = LogLevel.Information, Message = "JWT too long")]
    private partial void LogJwtTooLong();

    [LoggerMessage(Level = LogLevel.Information, Message = "token handle too long")]
    private partial void LogTokenHandleTooLong();
}
