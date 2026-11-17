// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Services.Default;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Covers the case where two identity resources differ only by case: a SAML SP allowed only the
/// lowercase name must not also pick up claim types from the uppercase one. Storage name lookups
/// (<c>Fields.Name.In(...)</c>) are case-insensitive, so every by-name lookup in
/// <see cref="Duende.IdentityServer.Stores.Storage.Resources.StorageResourceStore"/> must keep only
/// exact, ordinal-case matches.
/// </summary>
public sealed class SamlCaseSensitiveScopeResolutionTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task saml_resolution_for_lowercase_scope_excludes_uppercase_identity_resource_claims()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lower = $"profile_{suffix}";
        var upper = lower.ToUpperInvariant();

        await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = lower, UserClaims = ["lower_claim"] }, _ct);
        await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = upper, UserClaims = ["upper_claim"] }, _ct);

        var entityId = $"https://sp-{suffix}.example.com";
        var createResult = await _fixture.SamlServiceProviderAdmin.CreateAsync(new CreateSamlServiceProvider
        {
            EntityId = entityId,
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
            AllowedScopes = [lower]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var sp = await _fixture.SamlServiceProviderStore.FindByEntityIdAsync(entityId, _ct);
        sp.ShouldNotBeNull();

        var resolver = new DefaultSamlResourceResolver(_fixture.ResourceStore, NullLogger<DefaultSamlResourceResolver>.Instance);
        var resolution = await resolver.ResolveRequestedClaimTypesAsync(sp!, _ct);

        resolution.Succeeded.ShouldBeTrue();
        resolution.ClaimTypes.ShouldContain("lower_claim");
        resolution.ClaimTypes.ShouldNotContain("upper_claim");
    }
}
