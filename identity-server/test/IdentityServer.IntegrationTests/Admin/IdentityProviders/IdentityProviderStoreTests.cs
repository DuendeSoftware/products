// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityProviders;

// These tests verify that data saved in the admin interface
// is made available to the IIdentityProviderStore
public sealed class IdentityProviderStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IIdentityProviderAdmin NewAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();
    }

    private IIdentityProviderStore NewStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IIdentityProviderStore>();
    }

    [Fact]
    public async Task admin_create_then_store_get_by_scheme_returns_provider()
    {
        var admin = NewAdmin();
        var store = NewStore();

        var scheme = $"scheme_{Guid.NewGuid():N}";
        var config = new IdentityProviderConfiguration
        {
            Scheme = scheme,
            DisplayName = "Test OIDC",
            Enabled = true,
            Type = "test"
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var provider = await store.GetBySchemeAsync(scheme, _ct);

        provider.ShouldNotBeNull();
        provider.Scheme.ShouldBe(scheme);
        provider.DisplayName.ShouldBe("Test OIDC");
        provider.Enabled.ShouldBeTrue();
        provider.Type.ShouldBe("test");
    }

    [Fact]
    public async Task admin_create_oidc_then_store_returns_oidc_provider()
    {
        var admin = NewAdmin();
        var store = NewStore();

        var scheme = $"oidc_{Guid.NewGuid():N}";
        var config = new IdentityProviderConfiguration
        {
            Scheme = scheme,
            Type = "oidc",
            Properties = new Dictionary<string, string>
            {
                ["Authority"] = "https://idp.example.com",
                ["ClientId"] = "my-client"
            }
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var provider = await store.GetBySchemeAsync(scheme, _ct);

        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<OidcProvider>();

        var oidc = (OidcProvider)provider;
        oidc.Authority.ShouldBe("https://idp.example.com");
        oidc.ClientId.ShouldBe("my-client");
    }

    [Fact]
    public async Task admin_create_then_store_get_all_scheme_names_returns_name()
    {
        var admin = NewAdmin();
        var store = NewStore();

        var scheme = $"scheme_{Guid.NewGuid():N}";
        await admin.CreateAsync(
            new IdentityProviderConfiguration
            {
                Scheme = scheme,
                DisplayName = "All Schemes Test",
                Enabled = true,
                Type = "test"
            },
            _ct);

        var names = await store.GetAllSchemeNamesAsync(_ct);

        names.ShouldContain(n => n.Scheme == scheme);
        var name = names.First(n => n.Scheme == scheme);
        name.DisplayName.ShouldBe("All Schemes Test");
        name.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task disabled_provider_still_returned_by_store()
    {
        var admin = NewAdmin();
        var store = NewStore();

        var scheme = $"disabled_{Guid.NewGuid():N}";
        await admin.CreateAsync(
            new IdentityProviderConfiguration
            {
                Scheme = scheme,
                Enabled = false,
                Type = "test"
            },
            _ct);

        // GetByScheme should still return disabled providers
        var byScheme = await store.GetBySchemeAsync(scheme, _ct);
        byScheme.ShouldNotBeNull();
        byScheme.Enabled.ShouldBeFalse();

        // GetAllSchemeNames should include disabled providers
        var names = await store.GetAllSchemeNamesAsync(_ct);
        names.ShouldContain(n => n.Scheme == scheme);
        var name = names.First(n => n.Scheme == scheme);
        name.Enabled.ShouldBeFalse();
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
