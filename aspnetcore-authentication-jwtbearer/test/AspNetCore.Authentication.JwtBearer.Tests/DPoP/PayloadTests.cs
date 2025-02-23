// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Shouldly;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class PayloadTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_payload_fails()
    {
        Result.Payload = null;

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Missing payload");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_ath_fails()
    {
        Result.Payload = new Dictionary<string, object>();
        Result.Payload.ShouldNotContainKey(JwtClaimTypes.DPoPAccessTokenHash);

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'ath' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task mismatched_ath_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, "garbage that does not hash to the access token" }
        };

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'ath' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_jti_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
        };

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'jti' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_htm_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
        };

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'htm' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_htu_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
        };

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'htu' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task missing_iat_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, HttpUrl }
        };

        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'iat' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task expired_payload_fails()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, HttpUrl },
            { JwtClaimTypes.IssuedAt, IssuedAt },
        };

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew + 1));
        await ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'iat' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    public async Task valid_payload_succeeds()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, HttpUrl },
            { JwtClaimTypes.IssuedAt, IssuedAt }
        };

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));
        await ProofValidator.ValidatePayload(Context, Result);

        Result.IsError.ShouldBeFalse(Result.ErrorDescription);
    }
}