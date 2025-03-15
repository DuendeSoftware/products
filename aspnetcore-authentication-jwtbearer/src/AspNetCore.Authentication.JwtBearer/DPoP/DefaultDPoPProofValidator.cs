// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Default implementation of IDPoPProofValidator.
/// </summary>
public class DefaultDPoPProofValidator : IDPoPProofValidator
{
    private const string DataProtectorPurpose = "DPoPJwtBearerEvents-DPoPProofValidation-nonce";

    /// <summary>
    /// The signing algorithms supported for DPoP proofs.
    /// </summary>
    protected readonly static IEnumerable<string> SupportedSigningAlgorithms =
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512
    ];


    /// <summary>
    /// Provides the options for DPoP proof validation. 
    /// </summary>
    protected readonly IOptionsMonitor<DPoPOptions> OptionsMonitor;

    /// <summary>
    /// Protects and unprotects nonce values.
    /// </summary>
    protected readonly IDataProtector DataProtector;

    /// <summary>
    /// Caches proof tokens to detect replay.
    /// </summary>
    protected readonly IReplayCache ReplayCache;

    /// <summary>
    /// Clock for checking proof expiration.
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<DPoPJwtBearerEvents> Logger;

    /// <summary>
    /// Constructs a new instance of the <see cref="DefaultDPoPProofValidator"/>.
    /// </summary>
    public DefaultDPoPProofValidator(IOptionsMonitor<DPoPOptions> optionsMonitor,
        IDataProtectionProvider dataProtectionProvider, IReplayCache replayCache,
        TimeProvider timeProvider, ILogger<DPoPJwtBearerEvents> logger)
    {
        OptionsMonitor = optionsMonitor;
        DataProtector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        ReplayCache = replayCache;
        TimeProvider = timeProvider;
        Logger = logger;
    }

    /// <summary>
    /// Validates the DPoP proof.
    /// </summary>
    public async Task<DPoPProofValidationResult> Validate(DPoPProofValidationContext context, CancellationToken cancellationToken = default)
    {
        var result = new DPoPProofValidationResult();

        if (string.IsNullOrEmpty(context.ProofToken))
        {
            result.SetError("Missing DPoP proof value.");
            return result;
        }

        await ValidateHeader(context, result, cancellationToken);
        if (result.IsError)
        {
            Logger.LogDebug("Failed to validate DPoP header");
            return result;
        }

        await ValidateSignature(context, result, cancellationToken);
        if (result.IsError)
        {
            Logger.LogDebug("Failed to validate DPoP signature");
            return result;
        }

        await ValidatePayload(context, result);
        if (result.IsError)
        {
            Logger.LogDebug("Failed to validate DPoP payload");
            return result;
        }

        Logger.LogDebug("Successfully validated DPoP proof token");
        return result;
    }

    /// <summary>
    /// Validates the header.
    /// </summary>
    protected virtual Task ValidateHeader(
        DPoPProofValidationContext context,
        DPoPProofValidationResult result,
        CancellationToken cancellationToken = default)
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
            result.SetError("Malformed DPoP token.");
            return Task.CompletedTask;
        }

        if (!token.TryGetHeaderValue<string>("typ", out var typ) || typ != JwtClaimTypes.JwtTypes.DPoPProofToken)
        {
            Logger.LogDebug("Failed to get typ header");
            result.SetError("Invalid 'typ' value.");
            return Task.CompletedTask;
        }

        if (!token.TryGetHeaderValue<string>("alg", out var alg) || !SupportedSigningAlgorithms.Contains(alg))
        {
            Logger.LogDebug("Failed to get valid alg header");
            result.SetError("Invalid 'alg' value.");
            return Task.CompletedTask;
        }

        if (!token.TryGetHeaderValue<JsonElement>(JwtClaimTypes.JsonWebKey, out var jwkValues))
        {
            Logger.LogDebug("Failed to get jwk header");
            result.SetError("Invalid 'jwk' value.");
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
            result.SetError("Invalid 'jwk' value.");
            return Task.CompletedTask;
        }

        if (jwk.HasPrivateKey)
        {
            Logger.LogDebug("'jwk' value contains a private key.");
            result.SetError("'jwk' value contains a private key.");
            return Task.CompletedTask;
        }

        result.JsonWebKey = jwkJson;
        result.JsonWebKeyThumbprint = jwk.CreateThumbprint();

        var cnf = context.AccessTokenClaims.FirstOrDefault(c => c.Type == JwtClaimTypes.Confirmation);
        if (cnf is not { Value.Length: > 0 })
        {
            Logger.LogDebug("Empty cnf value in DPoP access token.");
            result.SetError("Missing 'cnf' value.");
            return Task.CompletedTask;
        }
        try
        {
            var cnfJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cnf.Value);
            if (cnfJson == null)
            {
                Logger.LogDebug("Null cnf value in DPoP access token.");
                result.SetError("Invalid 'cnf' value.");
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
            result.SetError("Invalid 'cnf' value.");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the signature.
    /// </summary>
    protected virtual async Task ValidateSignature(
        DPoPProofValidationContext context,
        DPoPProofValidationResult result,
        CancellationToken cancellationToken = default)
    {
        TokenValidationResult? tokenValidationResult = null;

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
            result.SetError("Invalid signature on DPoP token.");
        }

        if (tokenValidationResult?.Exception != null)
        {
            Logger.LogDebug("Error parsing DPoP token: {error}", tokenValidationResult.Exception.Message);
            result.SetError("Invalid signature on DPoP token.");
        }

        if (tokenValidationResult != null)
        {
            result.Payload = tokenValidationResult.Claims;
        }
    }

    /// <summary>
    /// Validates the payload.
    /// </summary>
    protected virtual async Task ValidatePayload(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
    {
        if (result.Payload is null)
        {
            result.SetError("Missing payload");
            return;
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.DPoPAccessTokenHash, out var ath))
        {
            result.AccessTokenHash = ath as string;
        }

        if (string.IsNullOrEmpty(result.AccessTokenHash))
        {
            result.SetError("Invalid 'ath' value.");
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(context.AccessToken);
        var hash = SHA256.HashData(bytes);

        var accessTokenHash = Base64Url.Encode(hash);
        if (accessTokenHash != result.AccessTokenHash)
        {
            result.SetError("Invalid 'ath' value.");
            return;
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.JwtId, out var jti))
        {
            if (jti is not string jtiString)
            {
                result.SetError("Invalid 'jti' value.");
                return;
            }
            var jtiBytes = Encoding.UTF8.GetBytes(jtiString);
            result.TokenIdHash = Base64Url.Encode(SHA256.HashData(jtiBytes));
        }

        if (string.IsNullOrEmpty(result.TokenIdHash))
        {
            result.SetError("Invalid 'jti' value.");
            return;
        }

        if (!result.Payload.TryGetValue(JwtClaimTypes.DPoPHttpMethod, out var htm) || !context.Method.Equals(htm))
        {
            result.SetError("Invalid 'htm' value.");
            return;
        }

        if (!result.Payload.TryGetValue(JwtClaimTypes.DPoPHttpUrl, out var htu) || !context.Url.Equals(htu))
        {
            result.SetError("Invalid 'htu' value.");
            return;
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.IssuedAt, out var iat))
        {
            result.IssuedAt = iat switch
            {
                int i => i,
                long l => l,
                _ => result.IssuedAt
            };
        }

        if (!result.IssuedAt.HasValue)
        {
            result.SetError("Invalid 'iat' value.");
            return;
        }

        if (result.Payload.TryGetValue(JwtClaimTypes.Nonce, out var nonce))
        {
            result.Nonce = nonce as string;
        }

        await ValidateFreshness(context, result, cancellationToken);
        if (result.IsError)
        {
            Logger.LogDebug("Failed to validate DPoP token freshness");
            return;
        }

        // we do replay at the end, so we only add to the reply cache if everything else is ok
        await ValidateReplay(context, result, cancellationToken);
        if (result.IsError)
        {
            Logger.LogDebug("Detected replay of DPoP token");
        }
    }

    /// <summary>
    /// Validates if the token has been replayed.
    /// </summary>
    protected virtual async Task ValidateReplay(
        DPoPProofValidationContext context,
        DPoPProofValidationResult result,
        CancellationToken cancellationToken = default)
    {
        var dPoPOptions = OptionsMonitor.Get(context.Scheme);

        if (await ReplayCache.Exists(result.TokenIdHash!, cancellationToken))
        {
            result.SetError("Detected DPoP proof token replay.");
            return;
        }

        // get the largest skew based on how client's freshness is validated
        var validateIat = dPoPOptions.ValidationMode != ExpirationValidationMode.Nonce;
        var validateNonce = dPoPOptions.ValidationMode != ExpirationValidationMode.IssuedAt;
        var skew = TimeSpan.Zero;
        if (validateIat && dPoPOptions.ClientClockSkew > skew)
        {
            skew = dPoPOptions.ClientClockSkew;
        }
        if (validateNonce && dPoPOptions.ServerClockSkew > skew)
        {
            skew = dPoPOptions.ServerClockSkew;
        }

        // we do x2 here because clock might be before or after, so we're making cache duration 
        // longer than the likelihood of proof token expiration, which is done before replay
        skew *= 2;
        var cacheDuration = dPoPOptions.ProofTokenValidityDuration + skew;
        var expiration = TimeProvider.GetUtcNow().Add(cacheDuration);
        await ReplayCache.Add(result.TokenIdHash!, expiration, cancellationToken);
    }

    /// <summary>
    /// Validates freshness of proofs.
    /// </summary>
    protected virtual async Task ValidateFreshness(
        DPoPProofValidationContext context,
        DPoPProofValidationResult result,
        CancellationToken cancellationToken = default)
    {
        var dPoPOptions = OptionsMonitor.Get(context.Scheme);

        var validateIat = dPoPOptions.ValidationMode != ExpirationValidationMode.Nonce;
        if (validateIat)
        {
            await ValidateIat(context, result, cancellationToken);
            if (result.IsError)
            {
                return;
            }
        }

        var validateNonce = dPoPOptions.ValidationMode != ExpirationValidationMode.IssuedAt;
        if (validateNonce)
        {
            await ValidateNonce(context, result, cancellationToken);
            if (result.IsError)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Validates the freshness of the iat value.
    /// </summary>
    protected virtual Task ValidateIat(
        DPoPProofValidationContext context,
        DPoPProofValidationResult result,
        CancellationToken _ = default)
    {
        // iat is required by an earlier validation, so result.IssuedAt will not be null
        if (IsExpired(context, result, result.IssuedAt!.Value, ExpirationValidationMode.IssuedAt))
        {
            result.SetError("Invalid 'iat' value.");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the freshness of the nonce value.
    /// </summary>
    protected virtual async Task ValidateNonce(
        DPoPProofValidationContext context,
        DPoPProofValidationResult result,
        CancellationToken _ = default)
    {
        if (string.IsNullOrWhiteSpace(result.Nonce))
        {
            result.SetError("Missing 'nonce' value.", OidcConstants.TokenErrors.UseDPoPNonce);
            result.ServerIssuedNonce = CreateNonce(context, result);
            return;
        }

        var time = await GetUnixTimeFromNonceAsync(context, result);
        if (time <= 0)
        {
            Logger.LogDebug("Invalid time value read from the 'nonce' value");

            result.SetError("Invalid 'nonce' value.", OidcConstants.TokenErrors.UseDPoPNonce);
            result.ServerIssuedNonce = CreateNonce(context, result);
            return;
        }

        if (IsExpired(context, result, time, ExpirationValidationMode.Nonce))
        {
            Logger.LogDebug("DPoP 'nonce' expired. Issuing new value to client.");

            result.SetError("Invalid 'nonce' value.", OidcConstants.TokenErrors.UseDPoPNonce);
            result.ServerIssuedNonce = CreateNonce(context, result);
            return;
        }
    }

    /// <summary>
    /// Creates a nonce value to return to the client.
    /// </summary>
    protected virtual string CreateNonce(DPoPProofValidationContext context, DPoPProofValidationResult result)
    {
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        return DataProtector.Protect(now.ToString());
    }

    /// <summary>
    /// Reads the time the nonce was created.
    /// </summary>
    protected virtual ValueTask<long> GetUnixTimeFromNonceAsync(DPoPProofValidationContext context, DPoPProofValidationResult result)
    {
        try
        {
            var value = DataProtector.Unprotect(result.Nonce!); // nonce is required by an earlier validation
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
    protected virtual bool IsExpired(DPoPProofValidationContext context, DPoPProofValidationResult result, long time,
        ExpirationValidationMode mode)
    {
        var dpopOptions = OptionsMonitor.Get(context.Scheme);
        var validityDuration = dpopOptions.ProofTokenValidityDuration;
        var skew = mode == ExpirationValidationMode.Nonce ? dpopOptions.ServerClockSkew
            : dpopOptions.ClientClockSkew;

        return IsExpired(validityDuration, skew, time);
    }

    internal bool IsExpired(TimeSpan validityDuration, TimeSpan clockSkew, long time)
    {
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var start = now + (int)clockSkew.TotalSeconds;
        if (start < time)
        {
            var diff = time - now;
            Logger.LogDebug("Expiration check failed. Creation time was too far in the future. The time being checked was {iat}, and clock is now {now}. The time difference is {diff}", time, now, diff);
            return true;
        }

        var expiration = time + (int)validityDuration.TotalSeconds;
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
