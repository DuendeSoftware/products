// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.Sp;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Saml2HandlerOptions = Duende.IdentityServer.Internal.Saml.Sp.AspNetCore.Saml2Options;

namespace UnitTests.Hosting;

public sealed class SamlStandaloneRegistrationTests
{
    private const string Category = "SAML Standalone Registration Tests";

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();
        return services;
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_registers_handler_for_default_scheme()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        });

        using var provider = services.BuildServiceProvider();
        var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        authOptions.SchemeMap.ShouldContainKey(SamlServiceProviderDefaults.Scheme);
        authOptions.SchemeMap[SamlServiceProviderDefaults.Scheme]
            .HandlerType.ShouldBe(typeof(Saml2Handler));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_registers_handler_for_custom_scheme()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider("custom-saml", opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        });

        using var provider = services.BuildServiceProvider();
        var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        authOptions.SchemeMap.ShouldContainKey("custom-saml");
        authOptions.SchemeMap["custom-saml"]
            .HandlerType.ShouldBe(typeof(Saml2Handler));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_registers_post_configure()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        });

        using var provider = services.BuildServiceProvider();
        var postConfigureOptions = provider.GetServices<IPostConfigureOptions<Saml2HandlerOptions>>();

        postConfigureOptions.OfType<PostConfigureSaml2Options>().ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_registers_configure_from_service_provider()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        });

        using var provider = services.BuildServiceProvider();
        var configureOptions = provider.GetServices<IConfigureOptions<Saml2HandlerOptions>>();

        configureOptions.OfType<ConfigureSaml2OptionsFromServiceProvider>().ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_handler_is_resolvable()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        });

        using var provider = services.BuildServiceProvider();
        var handler = provider.GetService<Saml2Handler>();

        handler.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_maps_sp_entity_id_to_handler_options()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();
        var options = optionsMonitor.Get(SamlServiceProviderDefaults.Scheme);

        options.SPOptions.EntityId.ShouldNotBeNull();
        options.SPOptions.EntityId.Id.ShouldBe("https://sp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_maps_idp_configuration()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.SingleLogoutServiceUrl = "https://idp.example.com/slo";
            opts.BindingType = SamlBindingType.HttpPost;
            opts.AllowUnsolicitedAuthnResponse = true;
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();
        var options = optionsMonitor.Get(SamlServiceProviderDefaults.Scheme);

        var idpEntityId = new Duende.IdentityServer.Internal.Saml.Sp.Metadata.EntityId("https://idp.example.com");
        var idp = options.IdentityProviders[idpEntityId];
        idp.ShouldNotBeNull();
        idp.SingleSignOnServiceUrl.ShouldBe(new Uri("https://idp.example.com/sso"));
        idp.SingleLogoutServiceUrl.ShouldBe(new Uri("https://idp.example.com/slo"));
        idp.AllowUnsolicitedAuthnResponse.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_multiple_schemes_have_isolated_configuration()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider("scheme-a", opts =>
        {
            opts.SpEntityId = "https://sp-a.example.com";
            opts.IdpEntityId = "https://idp-a.example.com";
            opts.SingleSignOnServiceUrl = "https://idp-a.example.com/sso";
            opts.ModulePath = "/Saml2-A";
        });
        services.AddAuthentication().AddSamlServiceProvider("scheme-b", opts =>
        {
            opts.SpEntityId = "https://sp-b.example.com";
            opts.IdpEntityId = "https://idp-b.example.com";
            opts.SingleSignOnServiceUrl = "https://idp-b.example.com/sso";
            opts.ModulePath = "/Saml2-B";
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        var optionsA = optionsMonitor.Get("scheme-a");
        var optionsB = optionsMonitor.Get("scheme-b");

        // Verify scheme A has its own configuration
        optionsA.SPOptions.EntityId.Id.ShouldBe("https://sp-a.example.com");
        optionsA.SPOptions.ModulePath.ShouldBe("/Saml2-A");
        var idpA = new Duende.IdentityServer.Internal.Saml.Sp.Metadata.EntityId("https://idp-a.example.com");
        optionsA.IdentityProviders[idpA].ShouldNotBeNull();

        // Verify scheme B has its own configuration
        optionsB.SPOptions.EntityId.Id.ShouldBe("https://sp-b.example.com");
        optionsB.SPOptions.ModulePath.ShouldBe("/Saml2-B");
        var idpB = new Duende.IdentityServer.Internal.Saml.Sp.Metadata.EntityId("https://idp-b.example.com");
        optionsB.IdentityProviders[idpB].ShouldNotBeNull();

        // Verify no cross-contamination: scheme A should NOT have scheme B's IdP
        var idpBInA = new Duende.IdentityServer.Internal.Saml.Sp.Metadata.EntityId("https://idp-b.example.com");
        Should.Throw<KeyNotFoundException>(() => optionsA.IdentityProviders[idpBInA]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_validates_missing_sp_entity_id()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.IdpEntityId = "https://idp.example.com";
            // SpEntityId intentionally not set
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_validates_missing_idp_entity_id()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            // IdpEntityId intentionally not set
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_validates_missing_single_sign_on_service_url()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            // SingleSignOnServiceUrl intentionally not set
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_validates_invalid_binding_type()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.BindingType = (SamlBindingType)99;
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_maps_idp_initiated_callback_url_to_handler_options()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AllowUnsolicitedAuthnResponse = true;
            opts.IdpInitiatedCallbackUrl = "/dashboard";
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();
        var options = optionsMonitor.Get(SamlServiceProviderDefaults.Scheme);

        options.SPOptions.IdpInitiatedCallbackUrl.ShouldNotBeNull();
        options.SPOptions.IdpInitiatedCallbackUrl.ToString().ShouldBe("/dashboard");
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_validates_invalid_idp_initiated_callback_url_scheme()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AllowUnsolicitedAuthnResponse = true;
            opts.IdpInitiatedCallbackUrl = "javascript:alert(1)";
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_maps_always_to_signing_behavior_always()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
            opts.SpSigningCertificateBase64 = ValidPkcs12Base64;
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();
        var options = optionsMonitor.Get(SamlServiceProviderDefaults.Scheme);

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Always);
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData(AuthnRequestSigningBehavior.Never)]
    public void AddSamlServiceProvider_maps_never_to_signing_behavior_never(AuthnRequestSigningBehavior behavior)
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AuthnRequestSigningBehavior = behavior;
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();
        var options = optionsMonitor.Get(SamlServiceProviderDefaults.Scheme);

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Never);
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_defaults_to_signing_behavior_never()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            // AuthnRequestSigningBehavior intentionally not set
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();
        var options = optionsMonitor.Get(SamlServiceProviderDefaults.Scheme);

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Never);
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSamlServiceProvider_validates_always_requires_sp_signing_certificate(string certBase64)
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
            opts.SpSigningCertificateBase64 = certBase64;
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        var ex = Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));

        ex.Message.ShouldContain(SamlServiceProviderDefaults.Scheme);
        ex.Message.ShouldContain("SpSigningCertificateBase64");
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_validates_invalid_authn_request_signing_behavior()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AuthnRequestSigningBehavior = (AuthnRequestSigningBehavior)99;
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        var ex = Should.Throw<OptionsValidationException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));

        ex.Message.ShouldContain("AuthnRequestSigningBehavior");
    }

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_malformed_sp_signing_certificate_passes_presence_validation_but_fails_loading()
    {
        var services = CreateServices();

        services.AddIdentityServer();
        services.AddAuthentication().AddSamlServiceProvider(opts =>
        {
            opts.SpEntityId = "https://sp.example.com";
            opts.IdpEntityId = "https://idp.example.com";
            opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            opts.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
            // Non-blank but not a valid base64/certificate value. This should pass the
            // presence-only validation done by SamlServiceProviderOptionsValidator, and
            // instead fail later when ConfigureSaml2OptionsFromServiceProvider attempts to
            // decode and load the certificate.
            opts.SpSigningCertificateBase64 = "not-a-valid-base64-certificate!!!";
        });

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2HandlerOptions>>();

        // Should not throw OptionsValidationException (presence validation passes)
        // but should fail during certificate loading in Configure().
        Should.Throw<FormatException>(() =>
            optionsMonitor.Get(SamlServiceProviderDefaults.Scheme));
    }

    // A self-signed PKCS#12 certificate (no password) generated for test purposes only.
    private const string ValidPkcs12Base64 =
        "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";
}
