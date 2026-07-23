// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.MultiSpace;

public sealed class SpaceAdminTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddMultiSpace();
        sc.AddStorageInternal(b => b.AddSqliteInMemoryStore());
        _services = sc.BuildServiceProvider();

        var schema = _services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(CancellationToken.None);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task can_create_space()
    {
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Test Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://example.com" }]
            },
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Id.ShouldNotBeNull();

        var getResult = await _admin.GetAsync(result.Id, CancellationToken.None);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.Name.ShouldBe("Test Space");
        getResult.Item.PoolId.Value.ShouldBeGreaterThan(0);
        getResult.Item.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task can_get_space_by_id()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space A",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://space-a.example.com" }]
            },
            CancellationToken.None);

        var retrieved = await _admin.GetAsync(created.Id!, CancellationToken.None);

        retrieved.Found.ShouldBeTrue();
        retrieved.Item.ShouldNotBeNull();
        retrieved.Item.Id.ShouldBe(created.Id!.Value);
        retrieved.Item.Name.ShouldBe("Space A");
    }

    [Fact]
    public async Task can_query_all_spaces()
    {
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space 1",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://space1.example.com" }]
            },
            CancellationToken.None);

        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space 2",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://space2.example.com" }]
            },
            CancellationToken.None);

        var all = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(),
            CancellationToken.None);

        all.Items.Count.ShouldBe(2);
        all.Items.ShouldContain(s => s.Name == "Space 1");
        all.Items.ShouldContain(s => s.Name == "Space 2");
    }

    [Fact]
    public async Task can_update_space()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Original Name",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update.example.com" }]
            },
            CancellationToken.None);

        var getResult = await _admin.GetAsync(created.Id!, CancellationToken.None);
        getResult.Found.ShouldBeTrue();

        var config = getResult.Item!;
        config.Name = "Updated Name";
        await _admin.UpdateAsync(created.Id!, config, getResult.Version!, CancellationToken.None);

        var retrieved = await _admin.GetAsync(created.Id!, CancellationToken.None);

        retrieved.Found.ShouldBeTrue();
        retrieved.Item!.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task can_delete_space()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "To Delete",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://delete.example.com" }]
            },
            CancellationToken.None);

        await _admin.DeleteAsync(created.Id!, CancellationToken.None);

        var retrieved = await _admin.GetAsync(created.Id!, CancellationToken.None);
        retrieved.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task create_with_duplicate_pattern_returns_error()
    {
        var pattern = new SpaceMatchPattern { Origin = "https://duplicate.example.com" };

        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "First",
                MatchPatterns = [pattern]
            },
            CancellationToken.None);

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second",
                MatchPatterns = [pattern]
            },
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task create_requires_at_least_one_pattern()
    {
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "No Patterns",
                MatchPatterns = []
            },
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task can_change_pool_id()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Pool Change Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool-change.example.com" }]
            },
            CancellationToken.None);

        var original = await _admin.GetAsync(created.Id!, CancellationToken.None);
        var originalPoolId = original.Item!.PoolId;

        var newPoolId = (PoolId)(originalPoolId.Value + 10);
        var changeResult = await _admin.ChangePoolIdAsync(created.Id!, newPoolId, CancellationToken.None);

        changeResult.IsSuccess.ShouldBeTrue();
        changeResult.Version!.Value.ShouldBeGreaterThan(created.Version!.Value);

        var updated = await _admin.GetAsync(created.Id!, CancellationToken.None);
        updated.Item!.PoolId.ShouldBe(newPoolId);
    }

    [Fact]
    public async Task can_create_space_with_explicit_pool_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Manual Pool Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://manual-pool.example.com" }],
                PoolId = (PoolId)42
            },
            ct);

        result.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(result.Id!, ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.PoolId.Value.ShouldBe(42);
    }

    [Fact]
    public async Task create_with_explicit_pool_id_rejects_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Zero Pool",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://zero-pool.example.com" }],
                PoolId = (PoolId)0
            },
            ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_with_duplicate_pool_id_returns_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "First Pool 99",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool99-first.example.com" }],
                PoolId = (PoolId)99
            },
            ct);

        first.IsSuccess.ShouldBeTrue();

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second Pool 99",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool99-second.example.com" }],
                PoolId = (PoolId)99
            },
            ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.Code == "validation_failed" &&
            e.PropertyNames != null &&
            e.Message.Contains("already in use", StringComparison.Ordinal) &&
            e.PropertyNames.Contains("PoolId"));
    }

    [Fact]
    public async Task change_pool_id_rejects_zero()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Pool Zero Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool-zero.example.com" }]
            },
            CancellationToken.None);

        var result = await _admin.ChangePoolIdAsync(created.Id!, (PoolId)0, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task change_pool_id_rejects_not_found()
    {
        var bogusId = (SpaceId)Guid.CreateVersion7();
        var result = await _admin.ChangePoolIdAsync(bogusId, (PoolId)5, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task update_cannot_change_pool_id()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Immutable Pool Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://immutable-pool.example.com" }]
            },
            CancellationToken.None);

        var getResult = await _admin.GetAsync(created.Id!, CancellationToken.None);
        var config = getResult.Item!;

        // Attempt to sneak a pool ID change through UpdateAsync
        var tampered = new SpaceConfiguration
        {
            Id = config.Id,
            Name = config.Name,
            Enabled = config.Enabled,
            PoolId = config.PoolId.Value + 1,
            MatchPatterns = config.MatchPatterns
        };

        var result = await _admin.UpdateAsync(created.Id!, tampered, getResult.Version!, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }
}
