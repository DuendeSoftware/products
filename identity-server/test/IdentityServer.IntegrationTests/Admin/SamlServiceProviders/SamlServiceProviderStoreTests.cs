// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Tests that data saved via ISamlServiceProviderAdmin is correctly
/// retrievable through ISamlServiceProviderStore.
/// </summary>
public sealed class SamlServiceProviderStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private ISamlServiceProviderAdmin BuildAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<ISamlServiceProviderAdmin>();
    }

    private ISamlServiceProviderStore BuildStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<ISamlServiceProviderStore>();
    }

    private static string GenerateSelfSignedCertBase64() =>
        SamlTestCertificates.GenerateSelfSignedCertBase64();

    [Fact]
    public async Task create_via_admin_then_find_by_entity_id_returns_model()
    {
        var admin = BuildAdmin();
        var store = BuildStore();

        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var certBase64 = GenerateSelfSignedCertBase64();

        var config = new SamlServiceProviderConfiguration
        {
            EntityId = entityId,
            Enabled = true,
            DisplayName = "Store Test SP",
            Description = "Round-trip test",
            ClockSkew = TimeSpan.FromMinutes(3),
            AssertionLifetime = TimeSpan.FromMinutes(10),
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            SingleLogoutServiceUrls =
            [
                new SamlEndpointConfiguration
                {
                    Location = "https://sp.example.com/slo",
                    Binding = SamlBinding.HttpRedirect
                }
            ],
            Certificates =
            [
                new SamlCertificateConfiguration { Base64Data = certBase64, Use = KeyUse.Signing }
            ],
            AllowIdpInitiated = true,
            AllowedScopes = ["openid", "profile"],
            ClaimMappings = new Dictionary<string, string> { ["email"] = "mail" },
            SigningBehavior = SamlSigningBehavior.SignBoth
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var sp = await store.FindByEntityIdAsync(entityId, _ct);

        sp.ShouldNotBeNull();
        sp.EntityId.ShouldBe(entityId);
        sp.Enabled.ShouldBeTrue();
        sp.DisplayName.ShouldBe("Store Test SP");
        sp.Description.ShouldBe("Round-trip test");
        sp.ClockSkew.ShouldBe(TimeSpan.FromMinutes(3));
        sp.AssertionLifetime.ShouldBe(TimeSpan.FromMinutes(10));
        sp.AssertionConsumerServiceUrls.Count.ShouldBe(1);
        sp.AssertionConsumerServiceUrls.First().Location.ShouldBe("https://sp.example.com/acs");
        sp.SingleLogoutServiceUrls.Count.ShouldBe(1);
        sp.Certificates.ShouldNotBeNull();
        sp.Certificates!.Count.ShouldBe(1);
        sp.Certificates.First().Certificate.HasPrivateKey.ShouldBeFalse();
        sp.Certificates.First().Use.ShouldBe(KeyUse.Signing);
        sp.AllowIdpInitiated.ShouldBeTrue();
        sp.AllowedScopes.ShouldContain("openid");
        sp.AllowedScopes.ShouldContain("profile");
        sp.ClaimMappings["email"].ShouldBe("mail");
        sp.SigningBehavior.ShouldBe(SamlSigningBehavior.SignBoth);
    }

    [Fact]
    public async Task find_by_entity_id_for_nonexistent_returns_null()
    {
        var store = BuildStore();
        var sp = await store.FindByEntityIdAsync("https://nonexistent.example.com", _ct);
        sp.ShouldBeNull();
    }

    [Fact]
    public async Task get_all_returns_all_created_service_providers()
    {
        var admin = BuildAdmin();
        var store = BuildStore();

        var entityId1 = $"https://sp1-{Guid.NewGuid():N}.example.com";
        var entityId2 = $"https://sp2-{Guid.NewGuid():N}.example.com";

        var config1 = new SamlServiceProviderConfiguration
        {
            EntityId = entityId1,
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp1.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        var config2 = new SamlServiceProviderConfiguration
        {
            EntityId = entityId2,
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp2.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        (await admin.CreateAsync(config1, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(config2, _ct)).IsSuccess.ShouldBeTrue();

        var allSps = new List<Models.SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            allSps.Add(sp);
        }

        allSps.ShouldContain(sp => sp.EntityId == entityId1);
        allSps.ShouldContain(sp => sp.EntityId == entityId2);
    }

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
