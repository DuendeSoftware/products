// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace Duende.IdentityServer.Validation;

public class AttestationSecretValidator(IIssuerNameService issuerNameService, IReplayCache replayCache, IOptions<IdentityServerOptions> options, ILogger<AttestationSecretValidator> logger) : ISecretValidator
{
    private const string ReplayCachePurpose = "ClientAttestationReplay-jti-";

    public async Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret)
    {
        var fail = new SecretValidationResult { Success = false };
        var success = new SecretValidationResult { Success = true };

        if (parsedSecret.Type != IdentityServerConstants.ParsedSecretTypes.AttestationBased || parsedSecret.Credential is not AttestationSecretValidationContext context)
        {
            return fail;
        }

        var (clientAttestationJwtWasValid, extractedJwk) = await ValidateClientAttestationJwt(context.ClientAttestationJwt, context.ClientId);
        if (!clientAttestationJwtWasValid || !await ValidateClientAttestationPopJwt(context.ClientAttestationPopJwt, context.ClientId, extractedJwk))
        {
            return fail;
        }

        return success;
    }

    private async Task<(bool, JsonWebKey)> ValidateClientAttestationJwt(string clientAttestationJwt, string clientId)
    {
        JsonWebToken token;
        var handler = new JsonWebTokenHandler();

        try
        {
            token = handler.ReadJsonWebToken(clientAttestationJwt);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error parsing Client Attestation JWT: {error}", ex.Message);
            return (false, null);
        }

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.TokenType, out var typ) || typ != "oauth-client-attestation+jwt") //TODO: replace with const from IdentityModel
        {
            logger.LogDebug("Invalid 'typ' value in Client Attestation JWT");
            return (false, null);
        }

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.Algorithm, out var alg) ||
            !IdentityServerConstants.SupportedAttestationBasedClientAuthSigningAlgorithms.Contains(alg))
        {
            logger.LogDebug("Invalid 'alg' value in Client Attestation JWT");
            return (false, null);
        }

        var issuerClaim = token.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Issuer);
        //TODO: how do we validate the identifier in this claim contains a unique identifier for the entity that issued the JWT? The issuer *should* be an identifier for the ClientAttester. A new config setting for the client?
        if (issuerClaim == null)
        {
            logger.LogDebug("Invalid issuer in Client Attestation JWT");
            return (false, null);
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "TODO",
            ValidateIssuer = false, //TODO: set true when we know how we're handling issuer
            ValidateAudience = false, //TODO: is it weird we don't expect an audience for this claim - the spec doesn't require it
            ValidateIssuerSigningKey = false, //TODO: this seems bad
            SignatureValidator = (jwtEncodedString, _) => new JsonWebToken(jwtEncodedString),
            ClockSkew = options.Value.JwtValidationClockSkew
        };
        var tokenValidationResult = await handler.ValidateTokenAsync(token, tokenValidationParameters);
        if (!tokenValidationResult.IsValid)
        {
            logger.LogDebug(tokenValidationResult.Exception, "Token validation failed: {message}",
                tokenValidationResult.Exception.Message);
            return (false, null);
        }

        var subClaim = token.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject);
        if (subClaim == null || !subClaim.Value.Equals(clientId))
        {
            logger.LogDebug("Invalid subject in Client Attestation JWT");
            return (false, null);
        }

        var cnfClaim = token.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Confirmation);
        var (cnfClaimWasValid, jwk) = ValidateCnfClaim(cnfClaim?.Value);
        if (!cnfClaimWasValid)
        {
            logger.LogDebug("Invalid cnf claim in Client Attestation JWT");
            return (false, null);
        }

        return (true, jwk);
    }

    private (bool, JsonWebKey) ValidateCnfClaim(string cnfClaimValue)
    {
        JsonWebKey jwk;
        var cnfJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cnfClaimValue);
        try
        {
            if (!cnfJson.TryGetValue("jwk", out var jwkElement))
            {
                logger.LogDebug("Missing jwk in cnf claim");
                return (false, null);
            }

            jwk = new Microsoft.IdentityModel.Tokens.JsonWebKey(jwkElement.GetRawText());
            if (jwk.HasPrivateKey)
            {
                logger.LogDebug("JWK in cnf claim contains private key");
                return (false, null);
            }

            //TODO: more validation?
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error parsing cnf claim");
            return (false, null);
        }

        return (true, jwk);
    }

    private async Task<bool> ValidateClientAttestationPopJwt(string clientAttestationPopJwt, string clientId, JsonWebKey extractedJwk)
    {
        JsonWebToken token;
        var handler = new JsonWebTokenHandler();

        try
        {
            token = handler.ReadJsonWebToken(clientAttestationPopJwt);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error parsing Client Attestation PoP JWT: {error}", ex.Message);
            return false;
        }

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.TokenType, out var typ) || typ != "oauth-client-attestation-pop+jwt")
        {
            logger.LogDebug("Invalid 'typ' value in Client Attestation PoP JWT");
            return false;
        }

        if (!token.TryGetHeaderValue<string>(JwtClaimTypes.Algorithm, out var alg) || !IdentityServerConstants.SupportedAttestationBasedClientAuthSigningAlgorithms.Contains(alg))
        {
            logger.LogDebug("Invalid 'alg' value in Client Attestation PoP JWT");
            return false;
        }

        var authorizationServerIdentity = await issuerNameService.GetCurrentAsync();
        var tokenValidationParameters = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = extractedJwk,
            ValidIssuer = clientId,
            ValidAudience = authorizationServerIdentity,
        };

        var result = await handler.ValidateTokenAsync(token, tokenValidationParameters);
        if (!result.IsValid)
        {
            logger.LogDebug(result.Exception, "Client Attestation PoP JWT failed validation: {message}", result.Exception.Message);
            return false;
        }

        var jtiClaim = token.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.JwtId);
        if (jtiClaim == null || string.IsNullOrWhiteSpace(jtiClaim.Value))
        {
            logger.LogDebug("Invalid jti in Client Attestation PoP JWT");
            return false;
        }

        var nonceClaim = token.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Nonce);
        if (nonceClaim == null) //TODO: nonce validation
        {
            return false;
        }

        if (!await JtiValueIsFresh(jtiClaim.Value, result.SecurityToken.ValidTo))
        {
            logger.LogDebug("Replay cache validation failed");
            return false;
        }

        return result.IsValid;
    }

    private async Task<bool> JtiValueIsFresh(string jti, DateTime expiration)
    {
        if (await replayCache.ExistsAsync(ReplayCachePurpose, jti))
        {
            logger.LogDebug("JTI has already been used");
            return false;
        }

        //var jtiExpiration = DateTimeOffset.FromUnixTimeSeconds(expiration);
        await replayCache.AddAsync(ReplayCachePurpose, jti, expiration);

        return true;
    }
}
