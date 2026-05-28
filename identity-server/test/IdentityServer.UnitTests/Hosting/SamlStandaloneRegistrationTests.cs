// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.Sp;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Saml.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Saml2HandlerOptions = Duende.IdentityServer.Internal.Saml.Sp.AspNetCore.Saml2Options;

namespace UnitTests.Hosting;

public sealed class SamlStandaloneRegistrationTests
{
    private const string Category = "SAML Standalone Registration Tests";

    [Fact]
    [Trait("Category", Category)]
    public void AddSamlServiceProvider_registers_handler_for_default_scheme()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

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
}
