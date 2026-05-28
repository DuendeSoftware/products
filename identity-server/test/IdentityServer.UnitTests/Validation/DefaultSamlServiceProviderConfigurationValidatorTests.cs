// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation;

public class DefaultSamlServiceProviderConfigurationValidatorTests
{
    private readonly DefaultSamlServiceProviderConfigurationValidator _validator = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static SamlServiceProvider ValidSp() =>
        new()
        {
            EntityId = "https://sp.example.com",
            AssertionConsumerServiceUrls = [new IndexedEndpoint { Location = "https://sp.example.com/acs", Index = 0, Binding = SamlBinding.HttpPost }],
            AllowedScopes = ["openid"]
        };

    [Fact]
    public async Task ValidSp_ShouldPassValidation()
    {
        var context = new SamlServiceProviderConfigurationValidationContext(ValidSp());

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyEntityId_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.EntityId = "";
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NoAssertionConsumerServiceUrls_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls = [];
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NullAssertionConsumerServiceUrls_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls = null!;
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task EmptyAllowedScopes_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AllowedScopes = [];
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NullAllowedScopes_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AllowedScopes = null!;
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NegativeAssertionLifetime_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AssertionLifetime = TimeSpan.FromSeconds(-1);
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ZeroAssertionLifetime_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AssertionLifetime = TimeSpan.Zero;
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NegativeClockSkew_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.ClockSkew = TimeSpan.FromSeconds(-1);
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ZeroClockSkew_ShouldPassValidation()
    {
        var sp = ValidSp();
        sp.ClockSkew = TimeSpan.Zero;
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NegativeRequestMaxAge_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.RequestMaxAge = TimeSpan.FromSeconds(-1);
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PositiveLifetimes_ShouldPassValidation()
    {
        var sp = ValidSp();
        sp.AssertionLifetime = TimeSpan.FromMinutes(5);
        sp.ClockSkew = TimeSpan.FromSeconds(30);
        sp.RequestMaxAge = TimeSpan.FromMinutes(10);
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task HttpRedirectAcsBinding_ShouldFailValidation()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls =
        [
            new IndexedEndpoint { Location = "https://sp.example.com/acs", Index = 0, Binding = SamlBinding.HttpRedirect }
        ];
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldBe("Assertion Consumer Service at index 0 uses an unsupported binding 'HttpRedirect'. Only HTTP-POST is supported for SAML Response delivery.");
    }

    [Fact]
    public async Task HttpPostAcsBinding_ShouldPassValidation()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls =
        [
            new IndexedEndpoint { Location = "https://sp.example.com/acs", Index = 0, Binding = SamlBinding.HttpPost }
        ];
        var context = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(context, _ct);

        context.IsValid.ShouldBeTrue();
    }
}
