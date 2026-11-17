// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.Storage;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Covers the client batch's missing-target path directly through <see cref="ClientRepository"/>: a
/// scope link whose target id doesn't exist must fail the whole batch with
/// <see cref="ClientBatchOutcomeKind.ScopeDoesNotExist"/> and roll back, for both an API scope link and
/// an identity resource link. <see cref="ClientAdmin"/> can't drive this path in practice (it only ever
/// resolves names it just found), so these tests drive the repository directly, as
/// ApiResourceAdminTests does for its own missing-target path.
/// </summary>
public sealed class ClientRepositoryMissingTargetTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private async Task<ClientDso.V1> CreateTemplateDsoAsync(string clientId)
    {
        var result = await _fixture.ClientAdmin.CreateAsync(new CreateClient { ClientId = clientId }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        var read = await _fixture.ClientRepository.TryReadByIdAsync(result.Id!.Value, _ct);
        read.ShouldNotBeNull();
        return read!.Value.Dso;
    }

    [Fact]
    public async Task create_with_nonexistent_api_scope_target_returns_scope_does_not_exist_and_rolls_back()
    {
        var repository = _fixture.ClientRepository;
        var templateClientId = $"client_{Guid.NewGuid():N}";
        var template = await CreateTemplateDsoAsync(templateClientId);

        var newClientId = $"client_{Guid.NewGuid():N}";
        var id = UuidV7.New();
        var dso = template with { Id = id.Value, ClientId = newClientId };
        var vanishedScopeName = $"scope_{Guid.NewGuid():N}";
        var vanishedScopeId = UuidV7.New().Value;

        var outcome = await repository.CreateAsync(id, dso, [new ScopeLink(vanishedScopeName, vanishedScopeId, ScopeLinkKind.ApiScope)], _ct);

        outcome.Kind.ShouldBe(ClientBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedScopeName);

        (await repository.TryReadByClientIdAsync(newClientId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task create_with_nonexistent_identity_resource_target_returns_scope_does_not_exist_and_rolls_back()
    {
        var repository = _fixture.ClientRepository;
        var templateClientId = $"client_{Guid.NewGuid():N}";
        var template = await CreateTemplateDsoAsync(templateClientId);

        var newClientId = $"client_{Guid.NewGuid():N}";
        var id = UuidV7.New();
        var dso = template with { Id = id.Value, ClientId = newClientId };
        var vanishedResourceName = $"resource_{Guid.NewGuid():N}";
        var vanishedResourceId = UuidV7.New().Value;

        var outcome = await repository.CreateAsync(id, dso, [new ScopeLink(vanishedResourceName, vanishedResourceId, ScopeLinkKind.IdentityResource)], _ct);

        outcome.Kind.ShouldBe(ClientBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedResourceName);

        (await repository.TryReadByClientIdAsync(newClientId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task update_with_nonexistent_scope_target_returns_scope_does_not_exist_and_rolls_back()
    {
        var repository = _fixture.ClientRepository;
        var clientId = $"client_{Guid.NewGuid():N}";
        var scopeName = $"scope_{Guid.NewGuid():N}";
        await _fixture.EnsureApiScopesAsync(scopeName);

        var createResult = await _fixture.ClientAdmin.CreateAsync(
            new CreateClient { ClientId = clientId, AllowedScopes = [scopeName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var id = createResult.Id!.Value;
        var existing = await repository.TryReadByIdAsync(id, _ct);
        existing.ShouldNotBeNull();
        var (existingDso, existingVersion) = existing!.Value;

        var attemptedDso = existingDso with { Enabled = false };
        var vanishedScopeName = $"scope_{Guid.NewGuid():N}";
        var vanishedScopeId = UuidV7.New().Value;

        var outcome = await repository.UpdateAsync(
            UuidV7.From(id),
            attemptedDso,
            expectedVersion: existingVersion,
            scopesToLink: [new ScopeLink(vanishedScopeName, vanishedScopeId, ScopeLinkKind.ApiScope)],
            scopesToUnlink: [],
            _ct);

        outcome.Kind.ShouldBe(ClientBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedScopeName);

        // Nothing was saved: version, fields and scope links are all unchanged.
        var current = await repository.TryReadByIdAsync(id, _ct);
        current.ShouldNotBeNull();
        current!.Value.Version.ShouldBe(existingVersion);
        current.Value.Dso.Enabled.ShouldBeTrue();
        var currentScopes = await repository.GetAllowedScopesAsync(id, _ct);
        currentScopes.Select(s => s.Name).ShouldBe([scopeName]);
    }
}
