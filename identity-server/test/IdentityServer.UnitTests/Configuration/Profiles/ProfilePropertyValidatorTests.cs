// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration.Profiles;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.UnitTests.Configuration.Profiles;

public class ProfilePropertyValidatorTests
{
    private const string Category = "ProfilePropertyValidator";

    [Fact]
    [Trait("Category", Category)]
    public void property_with_no_violation_should_pass()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = true;

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(false)
            .ViolatesIf(v => v == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        result.Passed.Count.ShouldBe(1);
        result.Failed.Count.ShouldBe(0);
        result.Passed.First().Path.ShouldBe("TestProperty");
        value.ShouldBeTrue(); // Should not have changed
    }

    [Fact]
    [Trait("Category", Category)]
    public void property_with_violation_and_profile_default_should_override()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = false;

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(false)
            .ViolatesIf(v => v == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        result.Passed.Count.ShouldBe(1);
        result.Failed.Count.ShouldBe(0);
        value.ShouldBeTrue(); // Should have been changed
    }

    [Fact]
    [Trait("Category", Category)]
    public void property_with_violation_and_no_profile_default_should_fail()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = false;

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(false)
            .ViolatesIf(v => v == false)
            .WarnWith("This property must be true")
            .Validate(result);

        result.IsValid.ShouldBeFalse();
        result.Passed.Count.ShouldBe(0);
        result.Failed.Count.ShouldBe(1);
        value.ShouldBeFalse(); // Should not have been changed
        result.Failed.First().Path.ShouldBe("TestProperty");
        result.Failed.First().Description.ShouldBe("This property must be true");
    }

    [Fact]
    [Trait("Category", Category)]
    public void timespan_property_with_max_value_violation_should_override()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = TimeSpan.FromMinutes(5);

        var validator = new ProfilePropertyValidator<TimeSpan>(
            "ClockSkew",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(TimeSpan.FromMinutes(5))
            .ViolatesIf(v => v > TimeSpan.FromSeconds(10))
            .OverrideWith(TimeSpan.FromSeconds(10))
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        value.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void non_default_value_with_violation_should_override()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = 100; // User explicitly set to 100 (default is 50)

        var validator = new ProfilePropertyValidator<int>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(50)
            .ViolatesIf(v => v > 60)
            .OverrideWith(60)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        value.ShouldBe(60); // Should override even though user set it
    }

    [Fact]
    [Trait("Category", Category)]
    public void nullable_string_with_null_value_should_detect_violation()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        string value = null;

        var validator = new ProfilePropertyValidator<string>(
            "IssuerUri",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(null)
            .ViolatesIf(v => string.IsNullOrEmpty(v))
            .WarnWith("IssuerUri must be configured")
            .Validate(result);

        result.IsValid.ShouldBeFalse();
        result.Failed.Count.ShouldBe(1);
        value.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void validate_without_violates_if_should_throw()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = true;

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        Should.Throw<InvalidOperationException>(() => validator.HasDefault(false).Validate(result));
    }

    [Fact]
    [Trait("Category", Category)]
    public void validate_without_default_should_throw()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = true;

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        Should.Throw<InvalidOperationException>(() =>
            validator.ViolatesIf(v => v == false).Validate(result));
    }

    [Fact]
    [Trait("Category", Category)]
    public void was_overridden_should_be_false_when_no_violation()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = true;

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(false)
            .ViolatesIf(v => v == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        result.Passed.Count.ShouldBe(1);
        result.Passed.First().WasOverridden.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void was_overridden_should_be_false_when_default_value_is_overridden()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = false; // This is the default value

        var validator = new ProfilePropertyValidator<bool>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(false)
            .ViolatesIf(v => v == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        result.Passed.Count.ShouldBe(1);
        result.Passed.First().WasOverridden.ShouldBeFalse(); // Not an override because it was the default
        value.ShouldBeTrue(); // Value should still be changed
    }

    [Fact]
    [Trait("Category", Category)]
    public void was_overridden_should_be_true_when_explicit_value_is_overridden()
    {
        var logger = new NullLogger<ProfilePropertyValidatorTests>();
        var result = new ProfileValidationResult();
        var value = 100; // User explicitly set to 100 (default is 50)

        var validator = new ProfilePropertyValidator<int>(
            "TestProperty",
            () => value,
            v => value = v,
            logger,
            logOverrides: true);

        validator
            .HasDefault(50)
            .ViolatesIf(v => v > 60)
            .OverrideWith(60)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        result.Passed.Count.ShouldBe(1);
        result.Passed.First().WasOverridden.ShouldBeTrue(); // Should be true because user's explicit value was overridden
        value.ShouldBe(60); // Value should be changed
    }
}
