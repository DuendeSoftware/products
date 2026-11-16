// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class ProofKeyBindingTests : DPoPProofValidatorTestBase
{
    [Fact]
    public async Task rejected_x5c_proof_does_not_affect_later_proofs_signed_by_bound_key()
    {
        using var clientKey = RSA.Create(2048);
        using var attackerKey = RSA.Create(2048);
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));
        var attackerContext = Context with
        {
            ProofToken = CreateProofSignedBy(new RsaSecurityKey(attackerKey), PublicJwkOf(clientKey, x5cKey: attackerKey)),
            AccessTokenClaims = [CnfClaimFor(attackerKey)]
        };
        _ = await ProofValidator.Validate(attackerContext);
        Context = Context with
        {
            ProofToken = CreateProofSignedBy(new RsaSecurityKey(clientKey), PublicJwkOf(clientKey, x5cKey: null)),
            AccessTokenClaims = [CnfClaimFor(clientKey)]
        };

        var proofValidation = await ProofValidator.Validate(Context);

        proofValidation.IsError.ShouldBeFalse(proofValidation.ErrorDescription);
    }

    [Fact]
    public async Task proof_signed_by_bound_key_succeeds_when_x5c_names_another_key()
    {
        using var clientKey = RSA.Create(2048);
        using var unrelatedKey = RSA.Create(2048);
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));
        Context = Context with
        {
            ProofToken = CreateProofSignedBy(new RsaSecurityKey(clientKey), PublicJwkOf(clientKey, x5cKey: unrelatedKey)),
            AccessTokenClaims = [CnfClaimFor(clientKey)]
        };

        var proofValidation = await ProofValidator.Validate(Context);

        proofValidation.IsError.ShouldBeFalse(proofValidation.ErrorDescription);
    }

    private static Dictionary<string, object> PublicJwkOf(RSA publicKey, RSA? x5cKey)
    {
        var parameters = publicKey.ExportParameters(false);
        var jwk = new Dictionary<string, object>
        {
            { JsonWebKeyParameterNames.Kty, JsonWebAlgorithmsKeyTypes.RSA },
            { JsonWebKeyParameterNames.E, Base64UrlEncoder.Encode(parameters.Exponent) },
            { JsonWebKeyParameterNames.N, Base64UrlEncoder.Encode(parameters.Modulus) },
        };
        if (x5cKey is not null)
        {
            var request = new CertificateRequest("CN=attacker", x5cKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            jwk.Add(JsonWebKeyParameterNames.X5c, new[] { Convert.ToBase64String(certificate.RawData) });
        }
        return jwk;
    }

    private static Claim CnfClaimFor(RSA key) =>
        new(JwtClaimTypes.Confirmation, JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(key.ExportParameters(false))).CreateThumbprintCnf());

    private static string CreateProofSignedBy(SecurityKey signingKey, object proofJwk)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            TokenType = JwtClaimTypes.JwtTypes.DPoPProofToken,
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(IssuedAt).UtcDateTime,
            AdditionalHeaderClaims = new Dictionary<string, object> { { JwtClaimTypes.JsonWebKey, proofJwk } },
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.DPoPHttpMethod, HttpMethod),
                new Claim(JwtClaimTypes.DPoPHttpUrl, HttpUrl),
                new Claim(JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash),
            ]),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
