// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.IdentityModel.Tokens;
using Duende.IdentityServer.Stores;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende.IdentityServer.Validation;

internal class TokenValidator : ITokenValidator
{
    private readonly ILogger _logger;
    private readonly IdentityServerOptions _options;
    private readonly IIssuerNameService _issuerNameService;
    private readonly IReferenceTokenStore _referenceTokenStore;
    private readonly ICustomTokenValidator _customValidator;
    private readonly IClientStore _clients;
    private readonly IProfileService _profile;
    private readonly IKeyMaterialService _keys;
    private readonly ISessionCoordinationService _sessionCoordinationService;
    private readonly IClock _clock;
    private readonly TokenValidationLog _log;

    public TokenValidator(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IClientStore clients,
        IProfileService profile,
        IReferenceTokenStore referenceTokenStore,
        ICustomTokenValidator customValidator,
        IKeyMaterialService keys,
        ISessionCoordinationService sessionCoordinationService,
        IClock clock,
        ILogger<TokenValidator> logger)
    {
        _options = options;
        _issuerNameService = issuerNameService;
        _clients = clients;
        _profile = profile;
        _referenceTokenStore = referenceTokenStore;
        _customValidator = customValidator;
        _keys = keys;
        _sessionCoordinationService = sessionCoordinationService;
        _clock = clock;
        _logger = logger;

        _log = new TokenValidationLog();
    }

    public async Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string? clientId = null,
        bool validateLifetime = true)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateIdentityToken");
        
        _logger.LogDebug("Start identity token validation");

        if (token.Length > _options.InputLengthRestrictions.Jwt)
        {
            _logger.LogError("JWT too long");
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        if (clientId.IsMissing())
        {
            clientId = GetClientIdFromJwt(token);

            if (clientId.IsMissing())
            {
                _logger.LogError("No clientId supplied, can't find id in identity token.");
                return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
            }
        }

        _log.ClientId = clientId;
        _log.ValidateLifetime = validateLifetime;

        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        if (client == null)
        {
            _logger.LogError("Unknown or disabled client: {clientId}.", clientId);
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        _log.ClientName = client.ClientName;
        _logger.LogDebug("Client found: {clientId} / {clientName}", client.ClientId, client.ClientName);

        var keys = await _keys.GetValidationKeysAsync();
        var result = await ValidateJwtAsync(token, keys, audience: clientId, validateLifetime: validateLifetime);

        result.Client = client;

        if (result.IsError)
        {
            LogError("Error validating JWT");
            return result;
        }

        _logger.LogDebug("Calling into custom token validator: {type}", _customValidator.GetType().FullName);
        var customResult = await _customValidator.ValidateIdentityTokenAsync(result);

        if (customResult.IsError)
        {
            LogError("Custom validator failed: " + (customResult.Error ?? "unknown"));
            return customResult;
        }

        _log.Claims = customResult.Claims.ToClaimsDictionary();

        LogSuccess();
        return customResult;
    }

    public async Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string? expectedScope = null)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateAccessToken");
        
        _logger.LogTrace("Start access token validation");

        _log.ExpectedScope = expectedScope;
        _log.ValidateLifetime = true;

        TokenValidationResult result;

        if (token.Contains('.'))
        {
            if (token.Length > _options.InputLengthRestrictions.Jwt)
            {
                _logger.LogError("JWT too long");

                return new TokenValidationResult
                {
                    IsError = true,
                    Error = OidcConstants.ProtectedResourceErrors.InvalidToken,
                    ErrorDescription = "Token too long"
                };
            }

            _log.AccessTokenType = AccessTokenType.Jwt.ToString();
            result = await ValidateJwtAsync(
                token,
                await _keys.GetValidationKeysAsync());
        }
        else
        {
            if (token.Length > _options.InputLengthRestrictions.TokenHandle)
            {
                _logger.LogError("token handle too long");

                return new TokenValidationResult
                {
                    IsError = true,
                    Error = OidcConstants.ProtectedResourceErrors.InvalidToken,
                    ErrorDescription = "Token too long"
                };
            }

            _log.AccessTokenType = AccessTokenType.Reference.ToString();
            result = await ValidateReferenceAccessTokenAsync(token);
        }

        _log.Claims = result.Claims.ToClaimsDictionary();

        if (result.IsError)
        {
            return result;
        }

        // make sure client is still active (if client_id claim is present)
        var clientClaim = result.Claims?.FirstOrDefault(c => c.Type == JwtClaimTypes.ClientId);
        if (clientClaim != null)
        {
            var client = await _clients.FindEnabledClientByIdAsync(clientClaim.Value);
            if (client == null)
            {
                _logger.LogError("Client deleted or disabled: {clientId}", clientClaim.Value);

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
            var principal = Principal.Create("tokenvalidator", result.Claims!.ToArray());

            if (result.ReferenceTokenId.IsPresent())
            {
                principal.Identities.First()
                    .AddClaim(new Claim(JwtClaimTypes.ReferenceTokenId, result.ReferenceTokenId));
            }

            var isActiveCtx = new IsActiveContext(principal, result.Client!,
                IdentityServerConstants.ProfileIsActiveCallers.AccessTokenValidation);
            await _profile.IsActiveAsync(isActiveCtx);

            if (isActiveCtx.IsActive == false)
            {
                _logger.LogError("User marked as not active: {subject}", subClaim.Value);

                result.IsError = true;
                result.Error = OidcConstants.ProtectedResourceErrors.InvalidToken;
                result.Claims = null;

                return result;
            }

            var sub = subClaim.Value;
            var sid = principal.FindFirstValue("sid");
            if (sid != null)
            {
                var sessionResult = await _sessionCoordinationService.ValidateSessionAsync(new SessionValidationRequest
                {
                    SubjectId = sub,
                    SessionId = sid,
                    Client = result.Client!,
                    Type = SessionValidationType.AccessToken
                });

                if (!sessionResult)
                {
                    _logger.LogError("Server-side session invalid for subject Id {subjectId} and session Id {sessionId}.", sub, sid);
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
                LogError($"Checking for expected scope {expectedScope} failed");
                return Invalid(OidcConstants.ProtectedResourceErrors.InsufficientScope);
            }
        }

        _logger.LogDebug("Calling into custom token validator: {type}", _customValidator.GetType().FullName);
        var customResult = await _customValidator.ValidateAccessTokenAsync(result);

        if (customResult.IsError)
        {
            LogError("Custom validator failed: " + (customResult.Error ?? "unknown"));
            return customResult;
        }

        // add claims again after custom validation
        _log.Claims = customResult.Claims.ToClaimsDictionary();

        LogSuccess();
        return customResult;
    }

    private async Task<TokenValidationResult> ValidateJwtAsync(string jwtString,
        IEnumerable<SecurityKeyInfo> validationKeys, bool validateLifetime = true, string? audience = null)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateJwt");
        
        var handler = new JsonWebTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = await _issuerNameService.GetCurrentAsync(),
            IssuerSigningKeys = validationKeys.Select(k => k.Key),
            ValidateLifetime = validateLifetime
        };

        if (audience.IsPresent())
        {
            parameters.ValidAudience = audience;
        }
        else
        {
            parameters.ValidateAudience = false;

            // if no audience is specified, we make at least sure that it is an access token
            if (_options.AccessTokenJwtType.IsPresent())
            {
                parameters.ValidTypes = new[] { _options.AccessTokenJwtType };
            }
        }
            
        var result = await handler.ValidateTokenAsync(jwtString, parameters);
        if (!result.IsValid)
        {
            if (result.Exception is SecurityTokenExpiredException expiredException)
            {
                _logger.LogInformation(expiredException, "JWT token validation error: {exception}",
                    expiredException.Message);
                return Invalid(OidcConstants.ProtectedResourceErrors.ExpiredToken);
            }
            else
            {
                _logger.LogError(result.Exception, "JWT token validation error: {exception}",
                    result.Exception.Message);
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
            client = await _clients.FindEnabledClientByIdAsync(clientId.Value);
            if (client == null)
            {
                LogError($"Client deleted or disabled: {clientId}");
                return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
            }
        }

        var claims = id.Claims.ToList();

        // check the scope format (array vs space delimited string)
        var scopes = claims.Where(c => c.Type == JwtClaimTypes.Scope).ToArray();
        foreach (var scope in scopes)
        {
            if (scope.Value.Contains(" "))
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

    private async Task<TokenValidationResult> ValidateReferenceAccessTokenAsync(string tokenHandle)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenValidator.ValidateReferenceAccessToken");
        
        _log.TokenHandle = tokenHandle;
        var token = await _referenceTokenStore.GetReferenceTokenAsync(tokenHandle);

        if (token == null)
        {
            LogError("Invalid reference token.");
            return Invalid(OidcConstants.ProtectedResourceErrors.InvalidToken);
        }

        if (token.CreationTime.HasExceeded(token.Lifetime, _clock.UtcNow.UtcDateTime))
        {
            LogError("Token expired.");

            await _referenceTokenStore.RemoveReferenceTokenAsync(tokenHandle);
            return Invalid(OidcConstants.ProtectedResourceErrors.ExpiredToken);
        }

        // load the client that is defined in the token
        Client? client = null;
        if (token.ClientId != null)
        {
            client = await _clients.FindEnabledClientByIdAsync(token.ClientId);
        }

        if (client == null)
        {
            LogError($"Client deleted or disabled: {token.ClientId}");
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

    private IEnumerable<Claim> ReferenceTokenToClaims(Token token)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Issuer, token.Issuer),
            new Claim(JwtClaimTypes.NotBefore,
                new DateTimeOffset(token.CreationTime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtClaimTypes.IssuedAt, new DateTimeOffset(token.CreationTime).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(JwtClaimTypes.Expiration,
                new DateTimeOffset(token.CreationTime).AddSeconds(token.Lifetime).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        if (!String.IsNullOrEmpty(token.Confirmation))
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
            _logger.LogError(ex, "Malformed JWT token: {exception}", ex.Message);
            return null;
        }
    }

    private TokenValidationResult Invalid(string error)
    {
        return new TokenValidationResult
        {
            IsError = true,
            Error = error
        };
    }

    private void LogError(string message)
    {
        _logger.LogError(message + "\n{@logMessage}", _log);
    }

    private void LogSuccess()
    {
        _logger.LogDebug("Token validation success\n{@logMessage}", _log);
    }
}