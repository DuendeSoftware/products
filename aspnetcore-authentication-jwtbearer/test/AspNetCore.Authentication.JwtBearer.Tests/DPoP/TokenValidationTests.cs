// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class TokenValidationTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task malformed_proof_tokens_fail()
    {
        Context = Context with { ProofToken = "This is obviously not a jwt" };

        await ProofValidator.ValidateToken(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid DPoP proof token.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task proof_tokens_with_incorrect_typ_header_fail()
    {
        Context = Context with { ProofToken = CreateDPoPProofToken(typ: "dpop+at") }; //Not dpop+jwt!
        ProofValidator.ValidateJwk(Context, Result); // Validate jwk first, as we need it to validate the token.
        
        await ProofValidator.ValidateToken(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid DPoP proof token.");
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
        ProofValidator.ValidateJwk(Context, Result); // Validate jwk first, as we need it to validate the token.

        await ProofValidator.ValidateToken(Context, Result);

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
        ProofValidator.ValidateJwk(Context, Result); // Validate jwk first, as we need it to validate the token.

        await ProofValidator.ValidateToken(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid DPoP proof token.");
    }
}
