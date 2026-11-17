// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.SamlServiceProviders;
using Duende.Storage;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Covers the SAML service provider batch's missing-target path directly through
/// <see cref="SamlServiceProviderRepository"/>: a scope link (identity resource) whose target id doesn't
/// exist must fail the whole batch with <see cref="SamlServiceProviderBatchOutcomeKind.ScopeDoesNotExist"/>
/// and roll back. <see cref="SamlServiceProviderAdmin"/> can't drive this path in practice (it only ever
/// resolves names it just found), so these tests drive the repository directly, as
/// ApiResourceAdminTests does for its own missing-target path.
/// </summary>
public sealed class SamlServiceProviderRepositoryMissingTargetTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    private static CreateSamlServiceProvider MinimalConfig(string entityId) =>
        new()
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
            AllowedScopes = []
        };

    private async Task<SamlServiceProviderDso.V1> CreateTemplateDsoAsync(string entityId)
    {
        var result = await _fixture.SamlServiceProviderAdmin.CreateAsync(MinimalConfig(entityId), _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        var read = await _fixture.SamlServiceProviderRepository.TryReadByIdAsync(result.Id!.Value, _ct);
        read.ShouldNotBeNull();
        return read!.Value.Dso;
    }

    [Fact]
    public async Task create_with_nonexistent_identity_resource_target_returns_scope_does_not_exist_and_rolls_back()
    {
        var repository = _fixture.SamlServiceProviderRepository;
        var templateEntityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var template = await CreateTemplateDsoAsync(templateEntityId);

        var newEntityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var id = UuidV7.New();
        var dso = template with { Id = id.Value, EntityId = newEntityId };
        var vanishedResourceName = $"resource_{Guid.NewGuid():N}";
        var vanishedResourceId = UuidV7.New().Value;

        var outcome = await repository.CreateAsync(id, dso, [new SamlAllowedScopeLink(vanishedResourceName, vanishedResourceId)], _ct);

        outcome.Kind.ShouldBe(SamlServiceProviderBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedResourceName);

        (await repository.TryReadByEntityIdAsync(newEntityId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task update_with_nonexistent_identity_resource_target_returns_scope_does_not_exist_and_rolls_back()
    {
        var repository = _fixture.SamlServiceProviderRepository;
        var resourceName = $"resource_{Guid.NewGuid():N}";
        await _fixture.EnsureIdentityResourcesAsync(resourceName);

        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var config = MinimalConfig(entityId);
        config.AllowedScopes = [resourceName];
        var createResult = await _fixture.SamlServiceProviderAdmin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var id = createResult.Id!.Value;
        var existing = await repository.TryReadByIdAsync(id, _ct);
        existing.ShouldNotBeNull();
        var (existingDso, existingVersion) = existing!.Value;

        var attemptedDso = existingDso with { Enabled = false };
        var vanishedResourceName = $"resource_{Guid.NewGuid():N}";
        var vanishedResourceId = UuidV7.New().Value;

        var outcome = await repository.UpdateAsync(
            UuidV7.From(id),
            attemptedDso,
            expectedVersion: existingVersion,
            scopesToLink: [new SamlAllowedScopeLink(vanishedResourceName, vanishedResourceId)],
            scopesToUnlink: [],
            _ct);

        outcome.Kind.ShouldBe(SamlServiceProviderBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedResourceName);

        // Nothing was saved: version, fields and scope links are all unchanged.
        var current = await repository.TryReadByIdAsync(id, _ct);
        current.ShouldNotBeNull();
        current!.Value.Version.ShouldBe(existingVersion);
        current.Value.Dso.Enabled.ShouldBeTrue();
        var currentScopes = await repository.GetAllowedScopesAsync(id, _ct);
        currentScopes.Select(s => s.Name).ShouldBe([resourceName]);
    }
}
