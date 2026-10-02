// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace UnitTests.Hosting;

public class SamlDynamicProviderTests
{
    private const string Category = "SAML Dynamic Provider Tests";

    // A self-signed PKCS#12 certificate (no password) generated for test purposes only.
    private const string ValidPkcs12Base64 =
        "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";

    private sealed class StubIssuerNameService(string issuer) : IIssuerNameService
    {
        public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(issuer);
    }

    private static (IServiceProvider Provider, DefaultHttpContext HttpContext) BuildDynamicSamlProvider(
        SamlProvider samlProvider,
        Action<IServiceCollection> additionalServices = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

        var httpContext = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();
        builder.AddInMemorySamlProviders([samlProvider]);
        services.Replace(ServiceDescriptor.Singleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com")));

        additionalServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        httpContext.RequestServices = provider;

        var cache = provider.GetRequiredService<DynamicAuthenticationSchemeCache>();
        cache.Add(samlProvider.Scheme!, new DynamicAuthenticationScheme(samlProvider, typeof(Saml2Handler)));

        return (provider, httpContext);
    }

    [Fact]
    [Trait("Category", Category)]
    public void add_saml_dynamic_provider_registers_saml_provider_type()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddSingleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com"));

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();

        using var provider = services.BuildServiceProvider();
        var isOptions = provider.GetRequiredService<IOptions<IdentityServerOptions>>().Value;

        var providerType = isOptions.DynamicProviders.FindProviderType("saml");

        providerType.ShouldNotBeNull();
        providerType!.HandlerType.ShouldBe(typeof(Saml2Handler));
        providerType.OptionsType.ShouldBe(typeof(Saml2Options));
        providerType.IdentityProviderType.ShouldBe(typeof(SamlProvider));
    }

    [Fact]
    [Trait("Category", Category)]
    public void in_memory_store_can_store_and_retrieve_saml_provider()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            DisplayName = "Test SAML IdP",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
        };

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddSingleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com"));

        // Provide a non-null HttpContext so NonCachingIdentityProviderStore proceeds past its guard.
        // RequestServices must be set so RemoveCacheEntry can call GetService without throwing.
        var httpContext = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();
        builder.AddInMemorySamlProviders([samlProvider]);

        using var provider = services.BuildServiceProvider();

        // Wire RequestServices after building the provider so the store's cache eviction has a container
        httpContext.RequestServices = provider;
        var store = provider.GetRequiredService<Duende.IdentityServer.Stores.IIdentityProviderStore>();

        var retrieved = store.GetBySchemeAsync("test-saml", default).GetAwaiter().GetResult();

        retrieved.ShouldNotBeNull();
        retrieved.Scheme.ShouldBe("test-saml");
        retrieved.Type.ShouldBe("saml");
        retrieved.ShouldBeOfType<SamlProvider>();

        var saml = (SamlProvider)retrieved;
        saml.IdpEntityId.ShouldBe("https://idp.example.com");
        saml.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_configure_options_is_registered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddHttpContextAccessor();
        services.AddSingleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com"));

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();

        using var provider = services.BuildServiceProvider();
        var configureOptions = provider.GetServices<IConfigureOptions<Saml2Options>>();

        configureOptions.ShouldNotBeNull();
        configureOptions.OfType<SamlConfigureOptions>().ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void post_configure_saml2_options_for_dynamic_is_registered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddSingleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com"));

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();

        using var provider = services.BuildServiceProvider();
        var postConfigureOptions = provider.GetServices<IPostConfigureOptions<Saml2Options>>();

        postConfigureOptions.ShouldNotBeNull();
        postConfigureOptions.OfType<PostConfigureSaml2OptionsForDynamic>().ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_configure_options_defaults_sp_entity_id_from_issuer_when_not_set()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

        var httpContext = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            DisplayName = "Test SAML IdP",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
        };

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();
        builder.AddInMemorySamlProviders([samlProvider]);
        services.Replace(ServiceDescriptor.Singleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com")));

        using var provider = services.BuildServiceProvider();
        httpContext.RequestServices = provider;

        var cache = provider.GetRequiredService<DynamicAuthenticationSchemeCache>();
        cache.Add("test-saml", new DynamicAuthenticationScheme(samlProvider, typeof(Saml2Handler)));

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<Saml2Options>>();
        var options = optionsMonitor.Get("test-saml");

        options.SPOptions.EntityId.ShouldNotBeNull();
        options.SPOptions.EntityId.Id.ShouldBe("https://issuer.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_configure_options_maps_always_to_signing_behavior_always_without_numeric_cast()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always,
            SpSigningCertificateBase64 = ValidPkcs12Base64,
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider);
        var options = provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml");

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Always);
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_configure_options_maps_never_to_signing_behavior_never_without_numeric_cast()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Never,
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider);
        var options = provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml");

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Never);
    }

    [Fact]
    [Trait("Category", Category)]
    public void missing_stored_behavior_flows_through_configuration_as_effective_never_and_does_not_require_signing()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            // AuthnRequestSigningBehavior intentionally left unset (missing property)
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider);
        var options = provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml");

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Never);
    }

    [Fact]
    [Trait("Category", Category)]
    public void corrupt_stored_behavior_survives_eager_store_round_trip_and_resolves_to_effective_never()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
        };
        // Corrupt the raw stored value directly, bypassing the setter's enum validation.
        samlProvider.Properties["AuthnRequestSigningBehavior"] = "TotallyBogusValue";

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();

        var httpContext = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();
        builder.AddInMemorySamlProviders([samlProvider]);
        services.Replace(ServiceDescriptor.Singleton<IIssuerNameService>(new StubIssuerNameService("https://issuer.example.com")));

        using var serviceProvider = services.BuildServiceProvider();
        httpContext.RequestServices = serviceProvider;

        // Simulate a store that eagerly evaluates model getters on retrieval.
        var store = serviceProvider.GetRequiredService<Duende.IdentityServer.Stores.IIdentityProviderStore>();
        var retrieved = (SamlProvider)store.GetBySchemeAsync("test-saml", default).GetAwaiter().GetResult()!;
        retrieved.AuthnRequestSigningBehavior.ShouldBe(AuthnRequestSigningBehavior.Never);

        var cache = serviceProvider.GetRequiredService<DynamicAuthenticationSchemeCache>();
        cache.Add("test-saml", new DynamicAuthenticationScheme(samlProvider, typeof(Saml2Handler)));

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml");
        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Never);
    }

    [Fact]
    [Trait("Category", Category)]
    public void callback_override_wins_over_stored_provider_value_for_authn_request_signing_behavior()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Never,
            SpSigningCertificateBase64 = ValidPkcs12Base64,
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider, services =>
        {
            services.Configure<SamlAuthenticationOptions>("test-saml", opts =>
                opts.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always);
        });

        var options = provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml");

        options.SPOptions.AuthenticateRequestSigningBehavior.ShouldBe(SigningBehavior.Always);
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void stored_always_with_blank_certificate_throws_and_names_scheme(string certBase64)
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always,
            SpSigningCertificateBase64 = certBase64,
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider);

        var ex = Should.Throw<InvalidOperationException>(() =>
            provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml"));

        ex.Message.ShouldContain("test-saml");
        ex.Message.ShouldContain("AuthnRequestSigningBehavior");
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void callback_effective_always_with_blank_certificate_throws_and_names_scheme(string certBase64)
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Never,
            SpSigningCertificateBase64 = certBase64,
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider, services =>
        {
            services.Configure<SamlAuthenticationOptions>("test-saml", opts =>
                opts.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always);
        });

        var ex = Should.Throw<InvalidOperationException>(() =>
            provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml"));

        ex.Message.ShouldContain("test-saml");
    }

    [Fact]
    [Trait("Category", Category)]
    public void malformed_nonblank_certificate_passes_presence_check_but_fails_certificate_loading()
    {
        var samlProvider = new SamlProvider
        {
            Scheme = "test-saml",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
            AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always,
            // Non-blank but not valid base64/certificate data. Passes the blank-check in
            // SamlConfigureOptions, then fails later during certificate decode/load.
            SpSigningCertificateBase64 = "not-a-valid-base64-certificate!!!",
        };

        var (provider, _) = BuildDynamicSamlProvider(samlProvider);

        Should.Throw<FormatException>(() =>
            provider.GetRequiredService<IOptionsMonitor<Saml2Options>>().Get("test-saml"));
    }
}
