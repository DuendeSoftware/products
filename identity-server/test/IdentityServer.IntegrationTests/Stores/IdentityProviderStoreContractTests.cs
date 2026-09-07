// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// A custom identity provider subtype used to verify that the store can
/// reconstruct user-defined derived types via the factory.
/// </summary>
internal record TestCustomProvider : IdentityProvider
{
    public TestCustomProvider() : base("test-custom") { }
    public TestCustomProvider(IdentityProvider other) : base("test-custom", other) { }

    public string? CustomProperty
    {
        get => this["CustomProperty"];
        set => this["CustomProperty"] = value;
    }
}

/// <summary>
/// Test implementation of <see cref="IIdentityProviderFactory"/> that handles
/// the built-in "oidc" and "saml" provider types as well as a custom type.
/// </summary>
internal class TestIdentityProviderFactory : IIdentityProviderFactory
{
    public IdentityProvider? Create(IdentityProvider baseModel) => baseModel.Type switch
    {
        "oidc" => new OidcProvider(baseModel),
        "saml" => new SamlProvider(baseModel),
        "test-custom" => new TestCustomProvider(baseModel),
        _ => null
    };
}

/// <summary>
/// Abstract contract tests for IIdentityProviderStore implementations.
/// Each derived class provides a specific store backend (EF, Storage).
/// </summary>
public abstract class IdentityProviderStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IIdentityProviderStore> CreateStore();

    /// <summary>
    /// Seeds an identity provider into the backing store.
    /// </summary>
    protected abstract Task SeedIdentityProviderAsync(IdentityProvider idp);

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetBySchemeAsync_should_find_by_scheme()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme1",
            Type = "oidc"
        };
        await SeedIdentityProviderAsync(idp);

        await using var handle = CreateStore();
        var item = await handle.Store.GetBySchemeAsync("scheme1", _ct);

        item.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies that providers with unrecognized types (factory returns null) are not returned.
    /// Virtual: Storage diverges — it returns the base IdentityProvider instead of null when
    /// the factory cannot create a typed instance. This is a known behavioral divergence that
    /// should be resolved in Storage production code separately.
    /// </summary>
    [Fact]
    public virtual async Task GetBySchemeAsync_should_filter_by_type()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme2",
            Type = "unknown"
        };
        await SeedIdentityProviderAsync(idp);

        await using var handle = CreateStore();
        var item = await handle.Store.GetBySchemeAsync("scheme2", _ct);

        item.ShouldBeNull();
    }

    [Fact]
    public async Task GetBySchemeAsync_should_return_saml_provider()
    {
        var idp = new SamlProvider
        {
            Scheme = "saml-scheme",
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso"
        };
        await SeedIdentityProviderAsync(idp);

        await using var handle = CreateStore();
        var item = await handle.Store.GetBySchemeAsync("saml-scheme", _ct);

        item.ShouldNotBeNull();
        item.ShouldBeOfType<SamlProvider>();
        var saml = (SamlProvider)item;
        saml.IdpEntityId.ShouldBe("https://idp.example.com");
        saml.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
    }

    [Fact]
    public async Task GetBySchemeAsync_should_filter_by_scheme_casing()
    {
        var idp = new OidcProvider
        {
            Scheme = "SCHEME3",
            Type = "oidc"
        };
        await SeedIdentityProviderAsync(idp);

        await using var handle = CreateStore();
        var item = await handle.Store.GetBySchemeAsync("scheme3", _ct);

        item.ShouldBeNull();
    }

    [Fact]
    public async Task GetBySchemeAsync_should_return_custom_provider()
    {
        var idp = new TestCustomProvider
        {
            Scheme = "custom-scheme",
            CustomProperty = "custom-value"
        };
        await SeedIdentityProviderAsync(idp);

        await using var handle = CreateStore();
        var item = await handle.Store.GetBySchemeAsync("custom-scheme", _ct);

        item.ShouldNotBeNull();
        item.ShouldBeOfType<TestCustomProvider>();
        var custom = (TestCustomProvider)item;
        custom.CustomProperty.ShouldBe("custom-value");
    }
}
