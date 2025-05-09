// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class PayloadTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public void missing_payload_fails()
    {
        Result.Payload = null;

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Missing payload");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void missing_ath_fails()
    {
        Result.Payload = new Dictionary<string, object>();
        Result.Payload.ShouldNotContainKey(JwtClaimTypes.DPoPAccessTokenHash);

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'ath' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void mismatched_ath_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, "garbage that does not hash to the access token" }
        };

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'ath' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void missing_jti_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
        };

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'jti' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void missing_htm_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
        };

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'htm' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void missing_htu_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
        };

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'htu' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Theory]
    [InlineData("https://example.com?query=1#fragment")]
    [InlineData("https://example.com/#fragment")]
    [InlineData("https://example.com/?query=1")]
    [Trait("Category", "Unit")]
    public void htu_ignores_query_and_fragment_parts_in_comparison_against_requested_url(string payloadUrl)
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, payloadUrl },
            { JwtClaimTypes.IssuedAt, IssuedAt }
        };

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));
        ProofValidator.ValidatePayload(Context, Result);

        Result.IsError.ShouldBeFalse(Result.ErrorDescription);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    [InlineData("https://EXAMPLE.com")]
    [InlineData("HtTpS://eXaMpLe.CoM")]
    [Trait("Category", "Unit")]
    public void htu_ignores_casing_in_comparison_against_requested_url(string payloadUrl)
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, payloadUrl },
            { JwtClaimTypes.IssuedAt, IssuedAt }
        };

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));
        ProofValidator.ValidatePayload(Context, Result);

        Result.IsError.ShouldBeFalse(Result.ErrorDescription);
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com:443")]
    [InlineData("http://example.com", "http://example.com:80")]
    [Trait("Category", "Unit")]
    public void htu_uses_scheme_based_normalization_in_comparison_against_requested_url(string expectedUrl, string payloadUrl)
    {
        Context = Context with { ExpectedUrl = expectedUrl };
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, payloadUrl },
            { JwtClaimTypes.IssuedAt, IssuedAt }
        };

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));
        ProofValidator.ValidatePayload(Context, Result);

        Result.IsError.ShouldBeFalse(Result.ErrorDescription);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void missing_iat_fails()
    {
        Result.Payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.DPoPAccessTokenHash, AccessTokenHash },
            { JwtClaimTypes.JwtId, TokenId },
            { JwtClaimTypes.DPoPHttpMethod, HttpMethod },
            { JwtClaimTypes.DPoPHttpUrl, HttpUrl }
        };

        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'iat' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void expired_payload_fails()
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
        ProofValidator.ValidatePayload(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'iat' value.");
        ProofValidator.ReplayCacheShouldNotBeCalled();
    }


    [Fact]
    [Trait("Category", "Unit")]
    public void valid_payload_succeeds()
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
        ProofValidator.ValidatePayload(Context, Result);

        Result.IsError.ShouldBeFalse(Result.ErrorDescription);
    }
}
