// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class FreshnessTests : DPoPProofValidatorTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public void can_retrieve_issued_at_unix_time_from_nonce()
    {
        var nonce = DataProtector.Protect(IssuedAt.ToString());

        var actual = NonceValidator.GetUnixTimeFromNonce(nonce);

        actual.ShouldBe(IssuedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void invalid_nonce_is_treated_as_zero()
    {
        var nonce = DataProtector.Protect("garbage that isn't a long");

        var actual = NonceValidator.GetUnixTimeFromNonce(nonce);

        actual.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void nonce_contains_data_protected_issued_at_unix_time()
    {
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        var actual = NonceValidator.CreateNonce(Context);

        DataProtector.Unprotect(actual).ShouldBe(IssuedAt.ToString());
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData(" ")]
    public void missing_nonce_returns_missing_result(string? nonce)
    {
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        var validationResult = NonceValidator.ValidateNonce(Context, nonce);

        validationResult.ShouldBe(NonceValidationResult.Missing);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("null")]
    [InlineData("garbage")]
    public void invalid_nonce_returns_invalid_result(string? nonce)
    {
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        var validationResult = NonceValidator.ValidateNonce(Context, nonce);

        validationResult.ShouldBe(NonceValidationResult.Invalid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void expired_nonce_returns_invalid_result()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ServerClockSkew = TimeSpan.FromSeconds(ClockSkew);

        // We go past validity and clock skew nonce to cause expiration
        var now = IssuedAt + ClockSkew + ValidFor + 1;

        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        var nonce = DataProtector.Protect(IssuedAt.ToString());

        var validationResult = NonceValidator.ValidateNonce(Context, nonce);

        validationResult.ShouldBe(NonceValidationResult.Invalid);
    }


    [Theory]
    [Trait("Category", "Unit")]
    // Around the maximum
    [InlineData(ClockSkew, IssuedAt + ValidFor + ClockSkew + 1, true)]
    [InlineData(ClockSkew, IssuedAt + ValidFor + ClockSkew, false)]
    [InlineData(ClockSkew, IssuedAt + ValidFor + ClockSkew - 1, false)]

    // Around the maximum, neglecting clock skew
    [InlineData(ClockSkew, IssuedAt + ValidFor - 1, false)]
    [InlineData(ClockSkew, IssuedAt + ValidFor, false)]
    [InlineData(ClockSkew, IssuedAt + ValidFor + 1, false)]
    // Around the maximum, with clock skew disabled
    [InlineData(0, IssuedAt + ValidFor - 1, false)]
    [InlineData(0, IssuedAt + ValidFor, false)]
    [InlineData(0, IssuedAt + ValidFor + 1, true)]
    public void expiration_check_is_correct_near_maximum(long clockSkew, long now, bool expected)
    {
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        var actual = ExpirationValidator.IsExpired(TimeSpan.FromSeconds(ValidFor), TimeSpan.FromSeconds(clockSkew), IssuedAt);
        actual.ShouldBe(expected);
        if (expected)
        {
            Logger.LogMessages.ShouldContain(msg => msg.StartsWith("Expiration check failed. Expiration has already happened."));
        }
    }

    // Around the minimum
    [InlineData(ClockSkew, IssuedAt - ClockSkew - 1, true)]
    [InlineData(ClockSkew, IssuedAt - ClockSkew, false)]
    [InlineData(ClockSkew, IssuedAt - ClockSkew + 1, false)]

    // Around the minimum, neglecting clock skew
    [InlineData(ClockSkew, IssuedAt - 1, false)]
    [InlineData(ClockSkew, IssuedAt, false)]
    [InlineData(ClockSkew, IssuedAt + 1, false)]

    // Around the minimum, with clock skew disabled
    [InlineData(0, IssuedAt - 1, true)]
    [InlineData(0, IssuedAt, false)]
    [InlineData(0, IssuedAt + 1, false)]
    public void expiration_check_is_correct_near_minimum(long clockSkew, long now, bool expected)
    {
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        var actual = ExpirationValidator.IsExpired(TimeSpan.FromSeconds(ValidFor), TimeSpan.FromSeconds(clockSkew), IssuedAt);
        actual.ShouldBe(expected);
        if (expected)
        {
            Logger.LogMessages.ShouldContain(msg => msg.StartsWith("Expiration check failed. Creation time was too far in the future."));
        }
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
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + 1));

        // We're not expired because we're using the right clock skew
        ProofValidator.IsExpired(Context, IssuedAt, mode).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void unexpired_proofs_do_not_set_errors()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.IssuedAt = IssuedAt;

        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt));

        ProofValidator.ValidateIat(Context, Result);

        Result.IsError.ShouldBeFalse();
        Result.Error.ShouldBeNull();
        Result.ErrorDescription.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void expired_proofs_set_errors()
    {
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.IssuedAt = IssuedAt;

        // Go forward into the future beyond the expiration and clock skew
        var now = IssuedAt + ClockSkew + ValidFor + 1;
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(now));

        ProofValidator.ValidateIat(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Invalid 'iat' value.");
    }

    [Theory]
    [InlineData(ExpirationValidationMode.IssuedAt)]
    [InlineData(ExpirationValidationMode.Both)]
    [Trait("Category", "Unit")]
    public void validate_iat_when_option_is_set(ExpirationValidationMode mode)
    {
        Options.ValidationMode = mode;
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ClientClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.IssuedAt = IssuedAt;
        if (mode == ExpirationValidationMode.Both)
        {
            Options.ServerClockSkew = TimeSpan.FromSeconds(ClockSkew);
            Result.Nonce = DataProtector.Protect(IssuedAt.ToString());
        }

        // Adjust time to exactly on the expiration
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew));

        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeFalse();

        // Now adjust time to one second later and try again
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew + 1));
        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ExpirationValidationMode.Nonce)]
    [InlineData(ExpirationValidationMode.Both)]
    [Trait("Category", "Unit")]
    public void validate_nonce_when_option_is_set(ExpirationValidationMode mode)
    {
        Options.ValidationMode = mode;
        Options.ProofTokenValidityDuration = TimeSpan.FromSeconds(ValidFor);
        Options.ServerClockSkew = TimeSpan.FromSeconds(ClockSkew);
        Result.Nonce = DataProtector.Protect(IssuedAt.ToString());
        if (mode == ExpirationValidationMode.Both)
        {
            Result.IssuedAt = IssuedAt;
        }

        // Adjust time to exactly on the expiration
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew));

        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeFalse();

        // Now adjust time to one second later and try again
        Clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(IssuedAt + ValidFor + ClockSkew + 1));
        ProofValidator.ValidateFreshness(Context, Result);
        Result.IsError.ShouldBeTrue();
    }
}
