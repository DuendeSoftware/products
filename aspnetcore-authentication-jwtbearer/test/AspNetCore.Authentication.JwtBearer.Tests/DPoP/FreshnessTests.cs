// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.DataProtection;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class FreshnessTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public void can_retrieve_issued_at_unix_time_from_nonce()
    {
        Result.Nonce = ProofValidator.TestDataProtector.Protect(IssuedAt.ToString());

        var actual = ProofValidator.GetUnixTimeFromNonce(Context, Result);

        actual.ShouldBe(IssuedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void invalid_nonce_is_treated_as_zero()
    {
        Result.Nonce = ProofValidator.TestDataProtector.Protect("garbage that isn't a long");

        var actual = ProofValidator.GetUnixTimeFromNonce(Context, Result);

        actual.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void nonce_contains_data_protected_issued_at_unix_time()
    {
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        var actual = ProofValidator.CreateNonce(Context, new DPoPProofValidationResult());

        ProofValidator.TestDataProtector.Unprotect(actual).ShouldBe(IssuedAt.ToString());
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData(" ")]
    public void missing_nonce_returns_use_dpop_nonce_with_server_issued_nonce(string? nonce)
    {
        Result.Nonce = nonce;
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        ProofValidator.ValidateNonce(Context, Result);

        Result.IsError.ShouldBeTrue();
        Result.Error.ShouldBe(OidcConstants.TokenErrors.UseDPoPNonce);
        Result.ErrorDescription.ShouldBe("Missing 'nonce' value.");
        Result.ServerIssuedNonce.ShouldNotBeNull();
        ProofValidator.TestDataProtector.Unprotect(Result.ServerIssuedNonce).ShouldBe(IssuedAt.ToString());
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("null")]
    [InlineData("garbage")]
    public void invalid_nonce_returns_use_dpop_nonce_with_server_issued_nonce(string? nonce)
    {
        Result.Nonce = nonce;
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        ProofValidator.ValidateNonce(Context, Result);

        Result.IsError.ShouldBeTrue();
        Result.Error.ShouldBe(OidcConstants.TokenErrors.UseDPoPNonce);
        Result.ErrorDescription.ShouldBe("Invalid 'nonce' value.");
        Result.ServerIssuedNonce.ShouldNotBeNull();
        ProofValidator.TestDataProtector.Unprotect(Result.ServerIssuedNonce).ShouldBe(IssuedAt.ToString());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void expired_nonce_returns_use_dpop_nonce_with_server_issued_nonce()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ServerClockSkew = TimeSpan.FromSeconds(ClockSkew);

        // We go past validity and clock skew nonce to cause expiration
        var now = IssuedAt + ClockSkew + ValidFor + 1;

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        Result.Nonce = ProofValidator.TestDataProtector.Protect(IssuedAt.ToString());

        ProofValidator.ValidateNonce(Context, Result);

        Result.IsError.ShouldBeTrue();
        Result.Error.ShouldBe(OidcConstants.TokenErrors.UseDPoPNonce);
        Result.ErrorDescription.ShouldBe("Invalid 'nonce' value.");
        Result.ServerIssuedNonce.ShouldNotBeNull();
        ProofValidator.TestDataProtector.Unprotect(Result.ServerIssuedNonce).ShouldBe(now.ToString());
    }


    [Theory]
    [Trait("Category", "Unit")]
    // Around the maximum
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + ValidFor + ClockSkew + 1, true)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + ValidFor + ClockSkew, false)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + ValidFor + ClockSkew - 1, false)]

    // Around the maximum, neglecting clock skew
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + ValidFor - 1, false)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + ValidFor, false)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + ValidFor + 1, false)]

    // Around the maximum, with clock skew disabled
    [InlineData(IssuedAt, ValidFor, 0, IssuedAt + ValidFor - 1, false)]
    [InlineData(IssuedAt, ValidFor, 0, IssuedAt + ValidFor, false)]
    [InlineData(IssuedAt, ValidFor, 0, IssuedAt + ValidFor + 1, true)]

    // Around the minimum
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt - ClockSkew - 1, true)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt - ClockSkew, false)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt - ClockSkew + 1, false)]

    // Around the minimum, neglecting clock skew
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt - 1, false)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt, false)]
    [InlineData(IssuedAt, ValidFor, ClockSkew, IssuedAt + 1, false)]

    // Around the minimum, with clock skew disabled
    [InlineData(IssuedAt, ValidFor, 0, IssuedAt - 1, true)]
    [InlineData(IssuedAt, ValidFor, 0, IssuedAt, false)]
    [InlineData(IssuedAt, ValidFor, 0, IssuedAt + 1, false)]
    public void expiration_check_is_correct_at_boundaries(long issuedAt, long validFor, long clockSkew, long now, bool expected)
    {
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        var actual = ProofValidator.IsExpired(TimeSpan.FromSeconds(validFor), TimeSpan.FromSeconds(clockSkew), issuedAt);
        actual.ShouldBe(expected);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ClockSkew, 0, ExpirationValidationMode.IssuedAt)]
    [InlineData(0, ClockSkew, ExpirationValidationMode.Nonce)]
    public void use_client_or_server_clock_skew_depending_on_validation_mode(int clientClockSkew, int serverClockSkew,
        ExpirationValidationMode mode)
    {
        Options.ClientClockSkew = TimeSpan.FromSeconds(clientClockSkew);
        Options.ServerClockSkew = TimeSpan.FromSeconds(serverClockSkew);
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);

        // We pick a time that needs some clock skew to be valid
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + 1));

        // We're not expired because we're using the right clock skew
        ProofValidator.IsExpired(Context, Result, IssuedAt, mode).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task unexpired_proofs_do_not_set_errors()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.IssuedAt = IssuedAt;

        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        ProofValidator.ValidateIat(Context, Result);

        Result.IsError.ShouldBeFalse();
        Result.Error.ShouldBeNull();
        Result.ErrorDescription.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task expired_proofs_set_errors()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.IssuedAt = IssuedAt;

        // Go forward into the future beyond the expiration and clock skew
        var now = IssuedAt + ClockSkew + ValidFor + 1;
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        ProofValidator.ValidateIat(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'iat' value.");
    }

    [Theory]
    [InlineData(ExpirationValidationMode.IssuedAt)]
    [InlineData(ExpirationValidationMode.Both)]
    [Trait("Category", "Unit")]
    public async Task validate_iat_when_option_is_set(ExpirationValidationMode mode)
    {
        Options.ValidationMode = mode;
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.IssuedAt = IssuedAt;
        if (mode == ExpirationValidationMode.Both)
        {
            Options.ServerClockSkew = TimeSpan.FromSeconds(ClockSkew);
            Result.Nonce = ProofValidator.TestDataProtector.Protect(IssuedAt.ToString());
        }

        // Adjust time to exactly on the expiration
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew));

        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeFalse();

        // Now adjust time to one second later and try again
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew + 1));
        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ExpirationValidationMode.Nonce)]
    [InlineData(ExpirationValidationMode.Both)]
    [Trait("Category", "Unit")]
    public async Task validate_nonce_when_option_is_set(ExpirationValidationMode mode)
    {
        Options.ValidationMode = mode;
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ServerClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.Nonce = ProofValidator.TestDataProtector.Protect(IssuedAt.ToString());
        if (mode == ExpirationValidationMode.Both)
        {
            Result.IssuedAt = IssuedAt;
        }

        // Adjust time to exactly on the expiration
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew));

        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeFalse();

        // Now adjust time to one second later and try again
        ProofValidator.TestTimeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew + 1));
        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeTrue();
    }
}
