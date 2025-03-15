// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class AccessTokenCnfTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_cnf_should_fail()
    {
        Context.AccessTokenClaims
            .ShouldNotContain(c => c.Type == JwtClaimTypes.Confirmation);

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Missing 'cnf' value.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task empty_cnf_value_should_fail()
    {
        Context = Context with { AccessTokenClaims = [new Claim(JwtClaimTypes.Confirmation, string.Empty)] };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Missing 'cnf' value.");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("not-a-json-object")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("3.14159")]
    [InlineData("[]")]
    [InlineData("[123]")]
    [InlineData("[\"asdf\"]")]
    [InlineData("null")]
    public async Task non_json_object_cnf_should_fail(string cnf)
    {
        Context = Context with { AccessTokenClaims = [new Claim(JwtClaimTypes.Confirmation, cnf)] };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'cnf' value.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task cnf_missing_jkt_should_fail()
    {
        var cnfObject = new Dictionary<string, string>
        {
            { "no-jkt-member-in-this-object", "causes-failure" }
        };
        Context = Context with { AccessTokenClaims = [new Claim(JwtClaimTypes.Confirmation, JsonSerializer.Serialize(cnfObject))] };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'cnf' value.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task mismatched_jkt_should_fail()
    {
        // Generate a new key, and use that in the access token's cnf claim
        // to simulate using the wrong key.
        Context = Context with { AccessTokenClaims = [CnfClaim(GenerateJwk())] };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'cnf' value.");
    }

    private static string GenerateJwk()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        jsonWebKey.Alg = "PS256";
        return JsonSerializer.Serialize(jsonWebKey);
    }
}
