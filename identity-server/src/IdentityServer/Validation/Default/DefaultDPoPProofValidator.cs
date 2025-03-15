// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default implementation of IDPoPProofValidator
/// </summary>
public class DefaultDPoPProofValidator : IDPoPProofValidator
{
    private const string ReplayCachePurpose = "DPoPReplay-jti-";
    private const string DataProtectorPurpose = "DPoPProofValidation-nonce";

    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The clock
    /// </summary>
    protected readonly IClock Clock;

    /// <summary>
    /// The replay cache
    /// </summary>
    protected IReplayCache ReplayCache;

    /// <summary>
    /// The data protection provider
    /// </summary>
    protected IDataProtector DataProtector { get; }

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// ctor
    /// </summary>
    public DefaultDPoPProofValidator(
        IdentityServerOptions options,
        IReplayCache replayCache,
        IClock clock,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DefaultDPoPProofValidator> logger)
    {
        Options = options;
        Clock = clock;
        ReplayCache = replayCache;
        DataProtector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        Logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DPoPProofValidatonResult> ValidateAsync(DPoPProofValidatonContext context)
    {
        var result = new DPoPProofValidatonResult() { IsError = false };

        try
        {
            if (string.IsNullOrEmpty(context?.ProofToken))
            {
                result.IsError = true;
                result.ErrorDescription = "Missing DPoP proof value.";
                return result;
            }

            await ValidateHeaderAsync(context, result);
            if (result.IsError)
            {
                Logger.LogDebug("Failed to validate DPoP header");
                return result;
            }

            await ValidateSignatureAsync(context, result);
            if (result.IsError)
            {
                Logger.LogDebug("Failed to validate DPoP signature");
                return result;
            }

            await ValidatePayloadAsync(context, result);
            if (result.IsError)
            {
                Logger.LogDebug("Failed to validate DPoP payload");
                return result;
            }

            Logger.LogDebug("Successfully validated DPoP proof token with thumbprint: {jkt}", result.JsonWebKeyThumbprint);
            result.IsError = false;
        }
        finally
        {
            if (result.IsError && result.Error.IsMissing())
            {
                result.Error = OidcConstants.TokenErrors.InvalidDPoPProof;
            }
        }

        return result;
    }

    /// <summary>
    /// Validates the header.
    /// </summary>
    protected virtual Task ValidateHeaderAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        JsonWebToken token;
        var handler = new JsonWebTokenHandler();

        try
        {
            token = handler.ReadJsonWebToken(context.ProofToken);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Error parsing DPoP token: {error}", ex.Message);
            result.IsError = true;
            result.ErrorDescription = "Malformed DPoP token.";
            return Task.CompletedTask;
        }

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.TokenType, out var typ) || typ != JwtClaimTypes.JwtTypes.DPoPProofToken)
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'typ' value.";
            return Task.CompletedTask;
        }

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.Algorithm, out var alg) || !IdentityServerConstants.SupportedDPoPSigningAlgorithms.Contains(alg))
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'alg' value.";
            return Task.CompletedTask;
        }

        if (!token.TryGetHeaderValue<JsonElement>(JwtClaimTypes.JsonWebKey, out var jwkValues))
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'jwk' value.";
            return Task.CompletedTask;
        }

        var jwkJson = JsonSerializer.Serialize(jwkValues);

        Microsoft.IdentityModel.Tokens.JsonWebKey jwk;
        try
        {
            jwk = new Microsoft.IdentityModel.Tokens.JsonWebKey(jwkJson);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Error parsing DPoP jwk value: {error}", ex.Message);
            result.IsError = true;
            result.ErrorDescription = "Invalid 'jwk' value.";
            return Task.CompletedTask;
        }

        if (jwk.HasPrivateKey)
        {
            result.IsError = true;
            result.ErrorDescription = "'jwk' value contains a private key.";
            return Task.CompletedTask;
        }

        result.JsonWebKey = jwkJson;
        result.JsonWebKeyThumbprint = jwk.CreateThumbprint();

        if (context.ValidateAccessToken)
        {
            var cnf = context.AccessTokenClaims.FirstOrDefault(c => c.Type == JwtClaimTypes.Confirmation);
            if (cnf is not { Value.Length: > 0 })
            {
                result.IsError = true;
                result.ErrorDescription = "Missing 'cnf' value.";
                return Task.CompletedTask;
            }
            try
            {
                var cnfJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cnf.Value);
                if (cnfJson == null)
                {
                    Logger.LogDebug("Null cnf value in DPoP access token.");
                    result.IsError = true;
                    result.ErrorDescription = "Missing 'cnf' value.";
                    return Task.CompletedTask;
                }
                else if (cnfJson.TryGetValue(JwtClaimTypes.ConfirmationMethods.JwkThumbprint, out var jktJson))
                {
                    var accessTokenJkt = jktJson.ToString();
                    if (accessTokenJkt == result.JsonWebKeyThumbprint)
                    {
                        result.Confirmation = cnf.Value;
                    }
                    else
                    {
                        Logger.LogDebug("jkt in DPoP access token does not match proof token key thumbprint.");
                    }
                }
                else
                {
                    Logger.LogDebug("jkt member missing from cnf claim in DPoP access token.");
                }
            }
            catch (JsonException e)
            {
                Logger.LogDebug("Failed to parse DPoP cnf claim: {JsonExceptionMessage}", e.Message);
            }

            if (result.Confirmation == null)
            {
                result.IsError = true;
                result.ErrorDescription = "Invalid 'cnf' value.";
            }
        }
        else
        {
            // The ValidateAccessToken == false case occurs when we are generating tokens. The confirmation value here
            // ultimately is put into the generated token's cnf claim.
            result.Confirmation = jwk.CreateThumbprintCnf();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the signature.
    /// </summary>
    protected virtual async Task ValidateSignatureAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        Microsoft.IdentityModel.Tokens.TokenValidationResult tokenValidationResult;

        try
        {
            var key = new Microsoft.IdentityModel.Tokens.JsonWebKey(result.JsonWebKey);
            var tvp = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateLifetime = false,
                IssuerSigningKey = key,
            };

            var handler = new JsonWebTokenHandler();
            tokenValidationResult = await handler.ValidateTokenAsync(context.ProofToken, tvp);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Error parsing DPoP token: {error}", ex.Message);
            result.IsError = true;
            result.ErrorDescription = "Invalid signature on DPoP token.";
            return;
        }

        if (tokenValidationResult.Exception != null)
        {
            Logger.LogDebug("Error parsing DPoP token: {error}", tokenValidationResult.Exception.Message);
            result.IsError = true;
            result.ErrorDescription = "Invalid signature on DPoP token.";
            return;
        }

        result.Payload = tokenValidationResult.Claims;
    }

    /// <summary>
    /// Validates the payload.
    /// </summary>
    protected virtual async Task ValidatePayloadAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        if (context.ValidateAccessToken)
        {
            if (result.Payload.TryGetValue(JwtClaimTypes.DPoPAccessTokenHash, out var ath))
            {
                result.AccessTokenHash = ath as string;
            }

            if (string.IsNullOrEmpty(result.AccessTokenHash))
            {
                result.IsError = true;
                result.ErrorDescription = "Invalid 'ath' value.";
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(context.AccessToken);
            var hash = SHA256.HashData(bytes);

            var accessTokenHash = Base64Url.Encode(hash);
            if (accessTokenHash != result.AccessTokenHash)
            {
                result.IsError = true;
                result.ErrorDescription = "Invalid 'ath' value.";
                return;
            }
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.JwtId, out var jti))
        {
            result.TokenId = jti as string;
        }

        if (string.IsNullOrEmpty(result.TokenId))
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'jti' value.";
            return;
        }

        if (!result.Payload.TryGetValue(JwtClaimTypes.DPoPHttpMethod, out var htm) || context.Method?.Equals(htm) == false)
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'htm' value.";
            return;
        }

        if (!result.Payload.TryGetValue(JwtClaimTypes.DPoPHttpUrl, out var htu) || context.Url?.Equals(htu) == false)
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'htu' value.";
            return;
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.IssuedAt, out var iat))
        {
            if (iat is int)
            {
                result.IssuedAt = (int)iat;
            }
            if (iat is long)
            {
                result.IssuedAt = (long)iat;
            }
        }

        if (!result.IssuedAt.HasValue)
        {
            result.IsError = true;
            result.ErrorDescription = "Missing 'iat' value.";
            return;
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.Nonce, out var nonce))
        {
            result.Nonce = nonce as string;
        }

        await ValidateFreshnessAsync(context, result);
        if (result.IsError)
        {
            Logger.LogDebug("Failed to validate DPoP token freshness");
            return;
        }

        // we do replay at the end so we only add to the reply cache if everything else is ok
        await ValidateReplayAsync(context, result);
        if (result.IsError)
        {
            result.ErrorDescription = "Detected replay of DPoP proof token.";
            return;
        }
    }

    /// <summary>
    /// Validates is the token has been replayed.
    /// </summary>
    protected virtual async Task ValidateReplayAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        if (await ReplayCache.ExistsAsync(ReplayCachePurpose, result.TokenId))
        {
            Logger.LogDebug("Detected DPoP proof token replay for jti {jti}", result.TokenId);
            result.IsError = true;
            return;
        }

        // get largest skew based on how client's freshness is validated
        var validateIat = (context.ExpirationValidationMode & DPoPTokenExpirationValidationMode.Iat) == DPoPTokenExpirationValidationMode.Iat;
        var validateNonce = (context.ExpirationValidationMode & DPoPTokenExpirationValidationMode.Nonce) == DPoPTokenExpirationValidationMode.Nonce;
        var skew = TimeSpan.Zero;
        if (validateIat && context.ClientClockSkew > skew)
        {
            skew = context.ClientClockSkew;
        }
        if (validateNonce && Options.DPoP.ServerClockSkew > skew)
        {
            skew = Options.DPoP.ServerClockSkew;
        }

        // we do x2 here because clock might be might be before or after, so we're making cache duration 
        // longer than the likelyhood of proof token expiration, which is done before replay
        skew *= 2;
        var cacheDuration = Options.DPoP.ProofTokenValidityDuration + skew;

        Logger.LogDebug("Adding proof token with jti {jti} to replay cache for duration {cacheDuration}", result.TokenId, cacheDuration);

        await ReplayCache.AddAsync(ReplayCachePurpose, result.TokenId, Clock.UtcNow.Add(cacheDuration));
    }

    /// <summary>
    /// Validates the freshness.
    /// </summary>
    protected virtual async Task ValidateFreshnessAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        var validateIat = (context.ExpirationValidationMode & DPoPTokenExpirationValidationMode.Iat) == DPoPTokenExpirationValidationMode.Iat;
        if (validateIat)
        {
            await ValidateIatAsync(context, result);
            if (result.IsError)
            {
                return;
            }
        }

        var validateNonce = (context.ExpirationValidationMode & DPoPTokenExpirationValidationMode.Nonce) == DPoPTokenExpirationValidationMode.Nonce;
        if (validateNonce)
        {
            await ValidateNonceAsync(context, result);
            if (result.IsError)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Validates the freshness of the iat value.
    /// </summary>
    protected virtual Task ValidateIatAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        if (IsExpired(context, result, context.ClientClockSkew, result.IssuedAt.Value))
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'iat' value.";
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the freshness of the nonce value.
    /// </summary>
    protected virtual async Task ValidateNonceAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        if (result.Nonce.IsMissing())
        {
            result.IsError = true;
            result.Error = OidcConstants.TokenErrors.UseDPoPNonce;
            result.ErrorDescription = "Missing 'nonce' value.";
            result.ServerIssuedNonce = CreateNonce(context, result);
            return;
        }

        var time = await GetUnixTimeFromNonceAsync(context, result);
        if (time <= 0)
        {
            Logger.LogDebug("Invalid time value read from the 'nonce' value");

            result.IsError = true;
            result.ErrorDescription = "Invalid 'nonce' value.";
            result.ServerIssuedNonce = CreateNonce(context, result);
            return;
        }

        if (IsExpired(context, result, Options.DPoP.ServerClockSkew, time))
        {
            Logger.LogDebug("DPoP 'nonce' expiration failed. It's possible that the server farm clocks might not be closely synchronized, so consider setting the ServerClockSkew on the DPoPOptions on the IdentityServerOptions.");

            result.IsError = true;
            result.ErrorDescription = "Invalid 'nonce' value.";
            result.ServerIssuedNonce = CreateNonce(context, result);
            return;
        }
    }

    /// <summary>
    /// Creates a nonce value to return to the client.
    /// </summary>
    /// <returns></returns>
    protected virtual string CreateNonce(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        var now = Clock.UtcNow.ToUnixTimeSeconds();
        return DataProtector.Protect(now.ToString());
    }

    /// <summary>
    /// Reads the time the nonce was created.
    /// </summary>
    /// <returns></returns>
    protected virtual ValueTask<long> GetUnixTimeFromNonceAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        try
        {
            var value = DataProtector.Unprotect(result.Nonce);
            if (long.TryParse(value, out var iat))
            {
                return ValueTask.FromResult(iat);
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Error parsing DPoP 'nonce' value: {error}", ex.ToString());
        }

        return ValueTask.FromResult<long>(0);
    }

    /// <summary>
    /// Validates the expiration of the DPoP proof.
    /// Returns true if the time is beyond the allowed limits, false otherwise.
    /// </summary>
    protected virtual bool IsExpired(DPoPProofValidatonContext context, DPoPProofValidatonResult result, TimeSpan clockSkew, long issuedAtTime)
    {
        var now = Clock.UtcNow.ToUnixTimeSeconds();
        var start = now + (int)clockSkew.TotalSeconds;
        if (start < issuedAtTime)
        {
            var diff = issuedAtTime - now;
            Logger.LogDebug("Expiration check failed. Creation time was too far in the future. The time being checked was {iat}, and clock is now {now}. The time difference is {diff}", issuedAtTime, now, diff);
            return true;
        }

        var expiration = issuedAtTime + (int)Options.DPoP.ProofTokenValidityDuration.TotalSeconds;
        var end = now - (int)clockSkew.TotalSeconds;
        if (expiration < end)
        {
            var diff = now - expiration;
            Logger.LogDebug("Expiration check failed. Expiration has already happened. The expiration was at {exp}, and clock is now {now}. The time difference is {diff}", expiration, now, diff);
            return true;
        }

        return false;
    }
}
