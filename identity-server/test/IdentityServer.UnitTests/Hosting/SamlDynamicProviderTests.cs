// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace UnitTests.Hosting;

public class SamlDynamicProviderTests
{
    private const string Category = "SAML Dynamic Provider Tests";

    private sealed class StubIssuerNameService(string issuer) : IIssuerNameService
    {
        public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(issuer);
    }

    [Fact]
    [Trait("Category", Category)]
    public void add_saml_dynamic_provider_registers_saml_provider_type()
    {
        var services = new ServiceCollection();
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
}
