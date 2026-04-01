// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Regression tests for GitHub issue #1667: concurrent DPoP proof validation
/// must not corrupt the shared TokenValidationParameters.IssuerSigningKey.
/// </summary>
public sealed class ConcurrentTokenValidationTests : DPoPProofValidatorTestBase
{
    [Fact]
    public async Task ConcurrentValidationsWithDifferentKeysShouldAllSucceed()
    {
        // Arrange – generate distinct RSA key pairs to simulate different DPoP clients
        const int keyCount = 10;
        const int requestsPerKey = 20;

        var keys = Enumerable.Range(0, keyCount)
            .Select(_ => GenerateRsaKeyPair())
            .ToList();

        // Build (context, result) pairs – each uses a proof token signed by a different key
        var validations = keys.SelectMany(key =>
            Enumerable.Range(0, requestsPerKey).Select(_ =>
            {
                var proofToken = CreateDPoPProofTokenForKey(key.PrivateJwk, key.PublicJwkPayload);
                var result = new DPoPProofValidationResult();
                var context = Context with { ProofToken = proofToken };

                // Pre-populate the JWK on the result, as ValidateJwk normally does
                ProofValidator.ValidateJwk(context, result);

                return (Context: context, Result: result);
            }))
            .ToList();

        // Act – run all validations concurrently against the shared Options.
        // Use a gate to ensure all tasks start simultaneously on the thread pool,
        // maximizing overlap to expose any race conditions.
        using var gate = new ManualResetEventSlim(false);

        var tasks = validations.Select(v => Task.Run(async () =>
        {
            gate.Wait();
            await ProofValidator.ValidateToken(v.Context, v.Result);
            return v.Result;
        })).ToArray();

        gate.Set();

        var results = await Task.WhenAll(tasks);

        // Assert – every validation must succeed; any failure indicates a race condition
        var failures = results.Where(r => r.IsError).ToList();
        failures.Count.ShouldBe(0,
            $"{failures.Count}/{results.Length} validations failed. " +
            $"First error: {failures.FirstOrDefault()?.ErrorDescription}");
    }

    private static (string PrivateJwk, Dictionary<string, string> PublicJwkPayload) GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);

        var privateJwkJson = JsonSerializer.Serialize(new
        {
            kty = "RSA",
            n = Base64UrlEncoder.Encode(parameters.Modulus!),
            e = Base64UrlEncoder.Encode(parameters.Exponent!),
            d = Base64UrlEncoder.Encode(parameters.D!),
            p = Base64UrlEncoder.Encode(parameters.P!),
            q = Base64UrlEncoder.Encode(parameters.Q!),
            dp = Base64UrlEncoder.Encode(parameters.DP!),
            dq = Base64UrlEncoder.Encode(parameters.DQ!),
            qi = Base64UrlEncoder.Encode(parameters.InverseQ!)
        });

        var publicJwkPayload = new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!)
        };

        return (privateJwkJson, publicJwkPayload);
    }

    private static string CreateDPoPProofTokenForKey(
        string privateJwkJson,
        Dictionary<string, string> publicJwkPayload)
    {
        var handler = new JsonWebTokenHandler();
        var signingKey = new JsonWebKey(privateJwkJson);
        var descriptor = new SecurityTokenDescriptor
        {
            TokenType = "dpop+jwt",
            IssuedAt = DateTime.UtcNow,
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { JwtClaimTypes.JsonWebKey, publicJwkPayload }
            },
            Subject = new ClaimsIdentity(),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };
        return handler.CreateToken(descriptor);
    }
}
