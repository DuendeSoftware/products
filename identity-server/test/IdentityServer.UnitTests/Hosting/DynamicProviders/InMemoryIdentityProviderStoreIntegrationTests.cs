// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Hosting.DynamicProviders;

public class InMemoryIdentityProviderStoreIntegrationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static (IServiceCollection Services, IIdentityServerBuilder Builder) CreateBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddIdentityServer();

        // AddIdentityServer() registers the OIDC dynamic provider type. Register the SAML dynamic
        // provider type as well so the configuration validator recognizes SAML providers when they
        // are resolved by scheme through the store. The in-memory provider store does not depend on
        // any storage backend, so no AddStorage() call is required.
        builder.AddSamlDynamicProvider();
        return (services, builder);
    }

    private async Task<IReadOnlyCollection<string>> GetSchemesAsync(IIdentityProviderStore store)
    {
        var names = await store.GetAllSchemeNamesAsync(_ct);
        return names.Select(s => s.Scheme).ToArray();
    }

    private static OidcProvider Oidc(string scheme) => new() { Scheme = scheme };

    private static SamlProvider Saml(string scheme) => new() { Scheme = scheme };

    private static OidcProvider ValidOidc(string scheme) => new()
    {
        Scheme = scheme,
        Authority = "https://example.com",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ResponseType = "code"
    };

    private static SamlProvider ValidSaml(string scheme) => new()
    {
        Scheme = scheme,
        IdpEntityId = "https://idp.example.com",
        SingleSignOnServiceUrl = "https://idp.example.com/sso",
        BindingType = "redirect"
    };

    [Fact]
    public async Task Can_add_oidc_providers_only()
    {
        var (services, builder) = CreateBuilder();

        builder.AddInMemoryOidcProviders([Oidc("oidc-a"), Oidc("oidc-b")]);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        (await GetSchemesAsync(store)).ShouldBe(["oidc-a", "oidc-b"], ignoreOrder: true);
    }

    [Fact]
    public async Task Can_add_oidc_providers_across_multiple_calls_deduplicated_by_scheme_first_wins()
    {
        var (services, builder) = CreateBuilder();

        // "oidc-shared" appears in both calls; the first registration must win.
        builder.AddInMemoryOidcProviders([Oidc("oidc-a"), Oidc("oidc-shared")]);
        builder.AddInMemoryOidcProviders([Oidc("oidc-shared"), Oidc("oidc-b")]);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        // The duplicate scheme appears exactly once.
        (await GetSchemesAsync(store)).ShouldBe(["oidc-a", "oidc-shared", "oidc-b"], ignoreOrder: true);
    }

    [Fact]
    public async Task Can_add_saml_providers()
    {
        var (services, builder) = CreateBuilder();

        builder.AddInMemorySamlProviders([Saml("saml-a"), Saml("saml-b")]);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        (await GetSchemesAsync(store)).ShouldBe(["saml-a", "saml-b"], ignoreOrder: true);
    }

    [Fact]
    public async Task Can_add_saml_providers_across_multiple_calls_deduplicated_by_scheme_first_wins()
    {
        var (services, builder) = CreateBuilder();

        // "saml-shared" appears in both calls; the first registration must win.
        builder.AddInMemorySamlProviders([Saml("saml-a"), Saml("saml-shared")]);
        builder.AddInMemorySamlProviders([Saml("saml-shared"), Saml("saml-b")]);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        (await GetSchemesAsync(store)).ShouldBe(["saml-a", "saml-shared", "saml-b"], ignoreOrder: true);
    }

    [Fact]
    public async Task Can_add_oidc_and_saml_providers_mixed()
    {
        var (services, builder) = CreateBuilder();

        builder.AddInMemoryOidcProviders([ValidOidc("oidc-a")]);
        builder.AddInMemorySamlProviders([ValidSaml("saml-a")]);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        (await GetSchemesAsync(store)).ShouldBe(["oidc-a", "saml-a"], ignoreOrder: true);

        (await store.GetBySchemeAsync("oidc-a", _ct)).ShouldBeOfType<OidcProvider>();
        (await store.GetBySchemeAsync("saml-a", _ct)).ShouldBeOfType<SamlProvider>();
    }

    [Fact]
    public async Task Providers_added_to_the_supplied_list_at_runtime_are_observed_by_the_store()
    {
        var (services, builder) = CreateBuilder();

        // Hidden/undocumented behavior: the supplied collection is retained by reference, so a caller
        // that keeps a reference to its List<IdentityProvider> can add providers after registration
        // (e.g. at runtime) and have them appear in the store without re-registering anything.
        var providers = new List<OidcProvider> { Oidc("oidc-a") };
        builder.AddInMemoryOidcProviders(providers);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        (await GetSchemesAsync(store)).ShouldBe(["oidc-a"]);

        // Mutate the originally-supplied list after the service provider was built.
        providers.Add(ValidOidc("oidc-added-at-runtime"));

        (await GetSchemesAsync(store)).ShouldBe(["oidc-a", "oidc-added-at-runtime"], ignoreOrder: true);

        var runtimeAdded = await store.GetBySchemeAsync("oidc-added-at-runtime", _ct);
        runtimeAdded.ShouldNotBeNull();
        runtimeAdded.Scheme.ShouldBe("oidc-added-at-runtime");
    }

    [Fact]
    public async Task Saml_providers_added_to_the_supplied_list_at_runtime_are_observed_by_the_store()
    {
        var (services, builder) = CreateBuilder();

        // The SAML registration path retains the supplied collection by reference (it no longer
        // copies via ToList()), so runtime mutation of the caller's list is observed by the store.
        var providers = new List<SamlProvider> { Saml("saml-a") };
        builder.AddInMemorySamlProviders(providers);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IIdentityProviderStore>();

        (await GetSchemesAsync(store)).ShouldBe(["saml-a"]);

        providers.Add(ValidSaml("saml-added-at-runtime"));

        (await GetSchemesAsync(store)).ShouldBe(["saml-a", "saml-added-at-runtime"], ignoreOrder: true);

        var runtimeAdded = await store.GetBySchemeAsync("saml-added-at-runtime", _ct);
        runtimeAdded.ShouldNotBeNull();
        runtimeAdded.Scheme.ShouldBe("saml-added-at-runtime");
    }

    [Fact]
    public void In_memory_registration_after_AddIdentityProviderStoreCache_does_not_shadow_the_configured_cache()
    {
        var (services, builder) = CreateBuilder();

        // Configure a caching in-memory store, then register more in-memory providers. The later
        // in-memory call must detect that the in-memory store was already registered and must NOT
        // append a non-caching store descriptor that would shadow the configured cache.
        builder.AddInMemoryOidcProviders([Oidc("oidc-a")]);
        builder.AddIdentityProviderStoreCache<InMemoryIdentityProviderStore>();
        builder.AddInMemoryOidcProviders([Oidc("oidc-b")]);

        // The last (resolved) IIdentityProviderStore descriptor must still be the caching wrapper.
        var lastStoreDescriptor = services.Last(d => d.ServiceType == typeof(IIdentityProviderStore));
        lastStoreDescriptor.ImplementationType.ShouldBe(
            typeof(CachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>));
    }

    [Fact]
    public void Multiple_in_memory_registration_calls_register_the_store_exactly_once()
    {
        var (services, builder) = CreateBuilder();

        builder.AddInMemoryOidcProviders([Oidc("oidc-a")]);
        builder.AddInMemorySamlProviders([Saml("saml-a")]);
        builder.AddInMemoryIdentityProviders([Oidc("oidc-b")]);

        // The marker must prevent the in-memory store (and admin-disabling) descriptor from being
        // appended more than once, regardless of how many in-memory registration calls are made.
        // AddIdentityServer() registers a soft NopIdentityProviderStore default, so only the
        // in-memory-backed store descriptor is counted here.
        services.Count(d =>
                d.ServiceType == typeof(IIdentityProviderStore) &&
                d.ImplementationType == typeof(NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>))
            .ShouldBe(1);
    }

    [Fact]
    public void In_memory_then_custom_store_then_in_memory_keeps_the_custom_store()
    {
        var (services, builder) = CreateBuilder();

        // A custom store registered after an in-memory registration intentionally shadows the
        // in-memory store (last-registration-wins). A later in-memory call must NOT re-select the
        // in-memory store, because the marker records that it was already registered; the custom
        // store was registered deliberately and must remain active.
        builder.AddInMemoryOidcProviders([Oidc("oidc-a")]);
        builder.AddIdentityProviderStore<CustomIdentityProviderStore>();
        builder.AddInMemoryOidcProviders([Oidc("oidc-b")]);

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IIdentityProviderStore>()
            .ShouldBeOfType<NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<CustomIdentityProviderStore>>>();
    }

    private sealed class CustomIdentityProviderStore : IIdentityProviderStore
    {
        public Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct) =>
            Task.FromResult<IReadOnlyCollection<IdentityProviderName>>([]);

        public Task<IdentityProvider?> GetBySchemeAsync(string scheme, Ct ct) =>
            Task.FromResult<IdentityProvider?>(null);
    }
}
