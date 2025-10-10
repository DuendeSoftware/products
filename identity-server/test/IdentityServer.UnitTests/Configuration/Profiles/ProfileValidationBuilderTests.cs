// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.Profiles;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.UnitTests.Configuration.Profiles;

public class ProfileValidationBuilderTests
{
    private const string Category = "ProfileValidationBuilder";

    [Fact]
    [Trait("Category", Category)]
    public void top_level_property_should_validate_correctly()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(5)
        };

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        builder.Property(
            "JwtValidationClockSkew",
            opt => opt.JwtValidationClockSkew,
            (opt, value) => opt.JwtValidationClockSkew = value)
            .HasDefault(TimeSpan.FromMinutes(5))
            .ViolatesIf(v => v > TimeSpan.FromSeconds(10))
            .OverrideWith(TimeSpan.FromSeconds(10))
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void nested_property_should_validate_correctly()
    {
        var options = new IdentityServerOptions();
        options.PushedAuthorization.Required = false;

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        builder.Property(
            "PushedAuthorization.Required",
            opt => opt.PushedAuthorization.Required,
            (opt, value) => opt.PushedAuthorization.Required = value)
            .HasDefault(false)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        options.PushedAuthorization.Required.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void multiple_validations_should_all_execute()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(5)
        };
        options.PushedAuthorization.Required = false;

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        // First validation
        builder.Property(
            "JwtValidationClockSkew",
            opt => opt.JwtValidationClockSkew,
            (opt, value) => opt.JwtValidationClockSkew = value)
            .HasDefault(TimeSpan.FromMinutes(5))
            .ViolatesIf(v => v > TimeSpan.FromSeconds(10))
            .OverrideWith(TimeSpan.FromSeconds(10))
            .Validate(result);

        // Second validation
        builder.Property(
            "PushedAuthorization.Required",
            opt => opt.PushedAuthorization.Required,
            (opt, value) => opt.PushedAuthorization.Required = value)
            .HasDefault(false)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        result.Passed.Count.ShouldBe(2);
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
        options.PushedAuthorization.Required.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void property_method_should_work_with_custom_accessors()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(5)
        };

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        builder.Property(
            "JwtValidationClockSkew",
            opt => opt.JwtValidationClockSkew,
            (opt, value) => opt.JwtValidationClockSkew = value)
            .HasDefault(TimeSpan.FromMinutes(5))
            .ViolatesIf(v => v > TimeSpan.FromSeconds(10))
            .OverrideWith(TimeSpan.FromSeconds(10))
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void deeply_nested_property_should_work()
    {
        var options = new IdentityServerOptions();
        options.PushedAuthorization.Lifetime = 1000;

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        builder.Property(
            "PushedAuthorization.Lifetime",
            opt => opt.PushedAuthorization.Lifetime,
            (opt, value) => opt.PushedAuthorization.Lifetime = value)
            .HasDefault(600)
            .ViolatesIf(value => value > 600)
            .OverrideWith(600)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        options.PushedAuthorization.Lifetime.ShouldBe(600);
    }

    [Fact]
    [Trait("Category", Category)]
    public void expression_syntax_should_work_for_top_level_property()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(5)
        };

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        builder.Property(opt => opt.JwtValidationClockSkew)
            .HasDefault(TimeSpan.FromMinutes(5))
            .ViolatesIf(v => v > TimeSpan.FromSeconds(10))
            .OverrideWith(TimeSpan.FromSeconds(10))
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void expression_syntax_should_work_for_nested_property()
    {
        var options = new IdentityServerOptions();
        options.PushedAuthorization.Required = false;

        var logger = new NullLogger<ProfileValidationBuilderTests>();
        var builder = new ProfileValidationBuilder<IdentityServerOptions>(options, logger, logOverrides: true);
        var result = new ProfileValidationResult();

        builder.Property(opt => opt.PushedAuthorization.Required)
            .HasDefault(false)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        result.IsValid.ShouldBeTrue();
        options.PushedAuthorization.Required.ShouldBeTrue();
    }
}
