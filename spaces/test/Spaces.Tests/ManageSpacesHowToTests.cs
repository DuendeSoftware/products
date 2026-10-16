// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.Spaces;

public sealed class ManageSpacesHowToTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = sc.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static Task<SaveResult<SpaceId>> CreateSpaceAsync(ISpaceAdmin admin, string name, string origin, Ct ct)
    {
        // begin-snippet: manage-spaces-create
        // With no explicit PoolId, Spaces auto-assigns one (the current maximum pool ID plus one).
        return admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            ct);
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task auto_assigned_pool_id_is_at_least_one()
    {
        var created = await CreateSpaceAsync(_admin, "Acme", "https://acme.example.com", _ct);
        created.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.PoolId.Value.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task explicit_pool_id_is_kept()
    {
        var name = "Globex";
        var origin = "https://globex.example.com";
        var poolId = (PoolId)42;
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: manage-spaces-create-with-explicit-pool-id
        // An explicit PoolId must be greater than zero and not already in use. Once a space is
        // created, its PoolId can never change: it is init-only on SpaceConfiguration.
        var created = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }],
                PoolId = poolId
            },
            ct);
        // end-snippet
        created.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.PoolId.Value.ShouldBe(42);
    }

    [Fact]
    public async Task can_get_and_update_a_space()
    {
        var created = await CreateSpaceAsync(_admin, "Acme", "https://acme-update.example.com", _ct);
        var id = created.Id!;
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: manage-spaces-get-and-update
        // GetAsync returns the current version alongside the item. Pass that version back to
        // UpdateAsync so the write fails instead of silently overwriting a concurrent change.
        var getResult = await admin.GetAsync(id, ct);

        var space = getResult.Item!;
        space.Name = "Acme (disabled)";
        space.Enabled = false;

        var updateResult = await admin.UpdateAsync(id, space, getResult.Version!, ct);
        // end-snippet
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterUpdate.Item!.Name.ShouldBe("Acme (disabled)");
        afterUpdate.Item.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task stale_version_conflicts_on_update()
    {
        var created = await CreateSpaceAsync(_admin, "Acme", "https://acme-stale.example.com", _ct);
        var firstRead = await _admin.GetAsync(created.Id!, _ct);

        // A second reader updates the space first, advancing its version.
        var secondRead = await _admin.GetAsync(created.Id!, _ct);
        secondRead.Item!.Name = "Updated elsewhere";
        var concurrentUpdate = await _admin.UpdateAsync(created.Id!, secondRead.Item, secondRead.Version!, _ct);
        concurrentUpdate.IsSuccess.ShouldBeTrue();

        // The first reader's stale version is now rejected.
        firstRead.Item!.Name = "Stale write";
        var staleUpdate = await _admin.UpdateAsync(created.Id!, firstRead.Item, firstRead.Version!, _ct);

        staleUpdate.IsSuccess.ShouldBeFalse();
        staleUpdate.Errors.ShouldContain(e => e.Code == "version_conflict");
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
#pragma warning disable CA1848 // The docs sample uses plain logging calls, not LoggerMessage delegates.
    private static async Task<SaveResult<SpaceId>> CreateSpaceOrLogErrorsAsync(
        ISpaceAdmin admin, CreateSpaceConfiguration configuration, ILogger logger, Ct ct)
    {
        // begin-snippet: manage-spaces-handle-save-errors
        var result = await admin.CreateAsync(configuration, ct);

        if (!result.IsSuccess)
        {
            // Errors is a StorageError collection: inspect Code (e.g. "validation_failed",
            // "already_exists") and PropertyNames to tell field-level problems apart.
            foreach (var error in result.Errors!)
            {
                var message = error.Code switch
                {
                    "already_exists" => "A match pattern is already claimed by another space.",
                    "validation_failed" => $"Validation failed: {error.Message}",
                    _ => error.Message
                };
                logger.LogWarning("Failed to create space {Name}: {Message}", configuration.Name, message);
            }
        }

        return result;
        // end-snippet
    }
#pragma warning restore CA1848
#pragma warning restore IDE0022

    [Fact]
    public async Task duplicate_match_pattern_returns_already_exists_error()
    {
        var logger = new FakeLogger<ManageSpacesHowToTests>();
        const string origin = "https://duplicate.example.com";
        await CreateSpaceOrLogErrorsAsync(
            _admin,
            new CreateSpaceConfiguration
            {
                Name = "First",
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            logger,
            _ct);

        var result = await CreateSpaceOrLogErrorsAsync(
            _admin,
            new CreateSpaceConfiguration
            {
                Name = "Second",
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            logger,
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "already_exists");
        logger.Collector.LatestRecord.Message.ShouldContain("already claimed by another space");
    }

    [Fact]
    public async Task empty_name_returns_validation_error()
    {
        var result = await CreateSpaceOrLogErrorsAsync(
            _admin,
            new CreateSpaceConfiguration
            {
                Name = "",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://empty-name.example.com" }]
            },
            new FakeLogger<ManageSpacesHowToTests>(),
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task deleted_space_patterns_and_pool_stay_reserved()
    {
        var pattern = new SpaceMatchPattern { Origin = "https://reserved.example.com" };
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Original", MatchPatterns = [pattern], PoolId = (PoolId)70 },
            _ct);
        var id = created.Id!;

        var deleteResult = await _admin.DeleteAsync(id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(id, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.IsDeleted.ShouldBeTrue();

        // Neither the pattern nor the pool can be reused by a new space while the old one
        // is only logically deleted.
        var reusePattern = await _admin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Reuse pattern", MatchPatterns = [pattern] }, _ct);
        reusePattern.IsSuccess.ShouldBeFalse();

        var reusePool = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Reuse pool",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://reuse-pool.example.com" }],
                PoolId = (PoolId)70
            },
            _ct);
        reusePool.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task deleted_space_can_be_undeleted()
    {
        var created = await CreateSpaceAsync(_admin, "Acme", "https://delete-undelete.example.com", _ct);
        var id = created.Id!;
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: manage-spaces-delete-and-undelete
        // DeleteAsync is a logical delete: it flips IsDeleted to true, but keeps the space's
        // match-pattern and pool reservations so nothing else can claim them until a purge.
        var deleteResult = await admin.DeleteAsync(id, ct);

        // UndeleteAsync restores a logically deleted space to active.
        var undeleteResult = await admin.UndeleteAsync(id, ct);
        // end-snippet
        deleteResult.IsSuccess.ShouldBeTrue();
        undeleteResult.IsSuccess.ShouldBeTrue();

        var afterUndelete = await _admin.GetAsync(id, _ct);
        afterUndelete.Item!.IsDeleted.ShouldBeFalse();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static Task<SaveResult<SpaceId>> PurgeSpaceAsync(ISpaceAdmin admin, SpaceId id, Ct ct)
    {
        // begin-snippet: manage-spaces-purge
        // PurgeAsync requires the space to be logically deleted first. It purges every entity
        // in the space's storage pool, then deletes the space record itself. This is irreversible.
        return admin.PurgeAsync(id, ct);
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task purge_releases_patterns_and_pool_for_reuse()
    {
        var pattern = new SpaceMatchPattern { Origin = "https://purge-release.example.com" };
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration { Name = "To Purge", MatchPatterns = [pattern], PoolId = (PoolId)80 },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);
        var purgeResult = await PurgeSpaceAsync(_admin, created.Id!, _ct);
        purgeResult.IsSuccess.ShouldBeTrue();

        var retrieved = await _admin.GetAsync(created.Id!, _ct);
        retrieved.Found.ShouldBeFalse();

        var reusePattern = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Reuse pattern after purge",
                MatchPatterns = [pattern],
                PoolId = (PoolId)80
            },
            _ct);
        reusePattern.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task purge_of_non_deleted_space_fails()
    {
        var created = await CreateSpaceAsync(_admin, "Still Active", "https://still-active.example.com", _ct);

        var result = await PurgeSpaceAsync(_admin, created.Id!, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }
}
