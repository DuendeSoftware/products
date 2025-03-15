// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DPoPApi;

public class DPoPProofValidator
{
    private const string ReplayCachePurpose = "DPoPJwtBearerEvents-DPoPReplay-jti-";
    private const string DataProtectorPurpose = "DPoPJwtBearerEvents-DPoPProofValidation-nonce";

    public readonly static IEnumerable<string> SupportedDPoPSigningAlgorithms = new[]
    {
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512
    };

    protected readonly IOptionsMonitor<DPoPOptions> OptionsMonitor;
    protected readonly IDataProtector DataProtector;
    protected readonly IReplayCache ReplayCache;
    protected readonly ILogger<DPoPJwtBearerEvents> Logger;

    public DPoPProofValidator(IOptionsMonitor<DPoPOptions> optionsMonitor, IDataProtectionProvider dataProtectionProvider, IReplayCache replayCache, ILogger<DPoPJwtBearerEvents> logger)
    {
        OptionsMonitor = optionsMonitor;
        DataProtector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        ReplayCache = replayCache;
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
            if (result.IsError && string.IsNullOrWhiteSpace(result.Error))
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

        try
        {
            var handler = new JsonWebTokenHandler();
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

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.Algorithm, out var alg) || !SupportedDPoPSigningAlgorithms.Contains(alg))
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

        JsonWebKey jwk;
        try
        {
            jwk = new JsonWebKey(jwkJson);
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
        result.Confirmation = jwk.CreateThumbprintCnf();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the signature.
    /// </summary>
    protected virtual async Task ValidateSignatureAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        TokenValidationResult tokenValidationResult;

        try
        {
            var key = new JsonWebKey(result.JsonWebKey);
            var tvp = new TokenValidationParameters
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

        using (var sha = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(context.AccessToken);
            var hash = sha.ComputeHash(bytes);

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

        if (!result.Payload.TryGetValue(JwtClaimTypes.DPoPHttpMethod, out var htm) || !context.Method.Equals(htm))
        {
            result.IsError = true;
            result.ErrorDescription = "Invalid 'htm' value.";
            return;
        }

        if (!result.Payload.TryGetValue(JwtClaimTypes.DPoPHttpUrl, out var htu) || !context.Url.Equals(htu))
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
            Logger.LogDebug("Detected replay of DPoP token");
            return;
        }
    }

    /// <summary>
    /// Validates is the token has been replayed.
    /// </summary>
    protected virtual async Task ValidateReplayAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        var dpopOptions = OptionsMonitor.Get(context.Scheme);

        if (await ReplayCache.ExistsAsync(ReplayCachePurpose, result.TokenId))
        {
            result.IsError = true;
            result.ErrorDescription = "Detected DPoP proof token replay.";
            return;
        }

        // get largest skew based on how client's freshness is validated
        var validateIat = dpopOptions.ValidateIat;
        var validateNonce = dpopOptions.ValidateNonce;
        var skew = TimeSpan.Zero;
        if (validateIat && dpopOptions.ClientClockSkew > skew)
        {
            skew = dpopOptions.ClientClockSkew;
        }
        if (validateNonce && dpopOptions.ServerClockSkew > skew)
        {
            skew = dpopOptions.ServerClockSkew;
        }

        // we do x2 here because clock might be might be before or after, so we're making cache duration 
        // longer than the likelyhood of proof token expiration, which is done before replay
        skew *= 2;
        var cacheDuration = dpopOptions.ProofTokenValidityDuration + skew;

        Logger.LogDebug("Adding proof token with jti {jti} to replay cache for duration {cacheDuration}", result.TokenId, cacheDuration);

        await ReplayCache.AddAsync(ReplayCachePurpose, result.TokenId, DateTimeOffset.UtcNow.Add(cacheDuration));
    }

    /// <summary>
    /// Validates the freshness.
    /// </summary>
    protected virtual async Task ValidateFreshnessAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
    {
        var dpopOptions = OptionsMonitor.Get(context.Scheme);

        var validateIat = dpopOptions.ValidateIat;
        if (validateIat)
        {
            await ValidateIatAsync(context, result);
            if (result.IsError)
            {
                return;
            }
        }

        var validateNonce = dpopOptions.ValidateNonce;
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
        var dpopOptions = OptionsMonitor.Get(context.Scheme);

        if (IsExpired(context, result, dpopOptions.ClientClockSkew, result.IssuedAt.Value))
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
        if (string.IsNullOrWhiteSpace(result.Nonce))
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

        var dpopOptions = OptionsMonitor.Get(context.Scheme);

        if (IsExpired(context, result, dpopOptions.ServerClockSkew, time))
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
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = now + (int)clockSkew.TotalSeconds;
        if (start < issuedAtTime)
        {
            var diff = issuedAtTime - now;
            Logger.LogDebug("Expiration check failed. Creation time was too far in the future. The time being checked was {iat}, and clock is now {now}. The time difference is {diff}", issuedAtTime, now, diff);
            return true;
        }

        var dpopOptions = OptionsMonitor.Get(context.Scheme);
        var expiration = issuedAtTime + (int)dpopOptions.ProofTokenValidityDuration.TotalSeconds;
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
