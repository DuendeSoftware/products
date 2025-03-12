// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class HeaderTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task malformed_proof_tokens_fail()
    {
        Context = Context with { ProofToken = "This is obviously not a jwt" };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Malformed DPoP token.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task proof_tokens_with_incorrect_typ_header_fail()
    {
        Context = Context with { ProofToken = CreateDPoPProofToken(typ: "at+jwt") }; //Not dpop+jwt!

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'typ' value.");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(SecurityAlgorithms.RsaSha256)]
    [InlineData(SecurityAlgorithms.RsaSha384)]
    [InlineData(SecurityAlgorithms.RsaSha512)]
    [InlineData(SecurityAlgorithms.RsaSsaPssSha256)]
    [InlineData(SecurityAlgorithms.RsaSsaPssSha384)]
    [InlineData(SecurityAlgorithms.RsaSsaPssSha512)]
    [InlineData(SecurityAlgorithms.EcdsaSha256)]
    [InlineData(SecurityAlgorithms.EcdsaSha384)]
    [InlineData(SecurityAlgorithms.EcdsaSha512)]
    public async Task valid_algorithms_succeed(string alg)
    {
        var useECAlgorithm = alg.StartsWith("ES");
        Context = Context with
        {
            ProofToken = CreateDPoPProofToken(alg: alg),
            AccessTokenClaims = [CnfClaim(useECAlgorithm ? PublicEcdsaJwk : PublicRsaJwk)]
        };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.IsError.ShouldBeFalse(Result.ErrorDescription);
    }


    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(SecurityAlgorithms.None)]
    [InlineData(SecurityAlgorithms.HmacSha256)]
    [InlineData(SecurityAlgorithms.HmacSha384)]
    [InlineData(SecurityAlgorithms.HmacSha512)]
    public async Task disallowed_algorithms_fail(string alg)
    {
        Context = Context with { ProofToken = CreateDPoPProofToken(alg: alg) };

        await ProofValidator.ValidateHeader(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'alg' value.");
    }
}
