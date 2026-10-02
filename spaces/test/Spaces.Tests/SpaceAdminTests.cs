// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceAdminTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly AttributeDefinition CompanyIdDef = new()
    {
        Code = AttributeCode.Create("company_id"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        IsUnique = true,
        IsQueryable = true
    };

    private static readonly AttributeDefinition ThemeDef = new()
    {
        Code = AttributeCode.Create("theme"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        IsUnique = false,
        IsQueryable = true
    };

    private static readonly AttributeDefinition PriorityDef = new()
    {
        Code = AttributeCode.Create("priority"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
        IsUnique = false,
        IsQueryable = true
    };

    private static readonly AttributeDefinition ActiveDef = new()
    {
        Code = AttributeCode.Create("active"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
        IsUnique = false,
        IsQueryable = false
    };

    private static readonly AttributeDefinition RateDef = new()
    {
        Code = AttributeCode.Create("rate"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Decimal),
        IsUnique = false,
        IsQueryable = false
    };

    private static readonly AttributeDefinition StartDateDef = new()
    {
        Code = AttributeCode.Create("start_date"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Date),
        IsUnique = false,
        IsQueryable = false
    };

    private static readonly AttributeDefinition CreatedAtDef = new()
    {
        Code = AttributeCode.Create("created_at"),
        AttributeType = new ScalarAttributeType(ScalarDataType.DateTime),
        IsUnique = false,
        IsQueryable = false
    };

    private static readonly AttributeDefinition SecondaryIdDef = new()
    {
        Code = AttributeCode.Create("secondary_id"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        IsUnique = true,
        IsQueryable = true
    };

    private static readonly AttributeDefinition TertiaryIdDef = new()
    {
        Code = AttributeCode.Create("tertiary_id"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        IsUnique = true,
        IsQueryable = true
    };

    private static readonly SchemaConfiguration SpaceSchema = new()
    {
        SchemaId = SchemaId.Space,
        DisplayName = "Space",
        Description = "Extended attributes for spaces.",
        AttributeDefinitions = [CompanyIdDef, ThemeDef, PriorityDef, ActiveDef, RateDef, StartDateDef, CreatedAtDef, SecondaryIdDef, TertiaryIdDef]
    };

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        sc.AddSingleton<ISchemaStore>(new InMemorySchemaStore([SpaceSchema]));
        _services = sc.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

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
            _ct);

        result.IsSuccess.ShouldBeTrue();
        result.Id.ShouldNotBeNull();

        var getResult = await _admin.GetAsync(result.Id, _ct);
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
            _ct);

        var retrieved = await _admin.GetAsync(created.Id!, _ct);

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
            _ct);

        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space 2",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://space2.example.com" }]
            },
            _ct);

        var all = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(),
            _ct);

        all.Items.Count.ShouldBe(2);
        all.Items.ShouldContain(s => s.Name == "Space 1");
        all.Items.ShouldContain(s => s.Name == "Space 2");
    }

    [Fact]
    public async Task query_includes_deleted_spaces()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Will Be Deleted",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://query-exclude.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);

        var all = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(),
            _ct);

        all.Items.ShouldContain(s => s.Name == "Will Be Deleted" && s.IsDeleted);

        var excludingDeleted = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(new SpaceFilter { IsDeleted = false }),
            _ct);

        excludingDeleted.Items.ShouldNotContain(s => s.Name == "Will Be Deleted");

        var onlyDeleted = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(new SpaceFilter { IsDeleted = true }),
            _ct);

        onlyDeleted.Items.ShouldContain(s => s.Name == "Will Be Deleted");
        onlyDeleted.Items.ShouldAllBe(s => s.IsDeleted);
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
            _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var config = getResult.Item!;
        config.Name = "Updated Name";
        await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);

        var retrieved = await _admin.GetAsync(created.Id!, _ct);

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
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);

        var retrieved = await _admin.GetAsync(created.Id!, _ct);
        retrieved.Found.ShouldBeTrue();
        retrieved.Item.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task delete_is_idempotent_on_already_deleted_space()
    {
        const string origin = "https://delete-twice.example.com";
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "To Delete Twice",
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);

        var afterFirstDelete = await _admin.GetAsync(created.Id!, _ct);
        afterFirstDelete.Found.ShouldBeTrue();
        afterFirstDelete.Item!.IsDeleted.ShouldBeTrue();
        var versionAfterFirstDelete = afterFirstDelete.Version;

        var cache = _services.GetRequiredService<HybridCache>();
        await cache.SetAsync(SpaceCacheKeys.ForSpaceId(created.Id!), afterFirstDelete.Item, cancellationToken: _ct);
        await cache.SetAsync(SpaceCacheKeys.ForPattern(origin, null), afterFirstDelete.Item, cancellationToken: _ct);
        await cache.SetAsync(SpaceCacheKeys.ForOriginClaim(origin), true, cancellationToken: _ct);

        var secondDelete = await _admin.DeleteAsync(created.Id!, _ct);
        secondDelete.IsSuccess.ShouldBeTrue();

        var afterSecondDelete = await _admin.GetAsync(created.Id!, _ct);
        afterSecondDelete.Found.ShouldBeTrue();
        afterSecondDelete.Item!.IsDeleted.ShouldBeTrue();
        afterSecondDelete.Version.ShouldBe(versionAfterFirstDelete);

        var spaceStore = _services.GetRequiredService<ISpaceStore>();
        (await spaceStore.TryGetSpace(created.Id!, _ct)).ShouldBeNull();
        (await spaceStore.TryResolveSpace(new SpaceMatchPattern { Origin = origin }, _ct)).ShouldBeNull();
        (await spaceStore.IsOriginClaimed(origin, _ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task can_update_deleted_space_metadata()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted Space Original",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update-deleted.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.IsDeleted.ShouldBeTrue();

        var newProps = new AttributeValueCollection();
        newProps.Set(ThemeDef.Code, "dark");

        var config = getResult.Item;
        config.Name = "Deleted Space Renamed";
        config.Enabled = false;
        config.ExtendedProperties = newProps.ToList();

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.Name.ShouldBe("Deleted Space Renamed");
        afterUpdate.Item.Enabled.ShouldBeFalse();
        afterUpdate.Item.ExtendedProperties.OfType<AttributeValue<string>>()
            .First(a => a.Code == ThemeDef.Code).TypedValue.ShouldBe("dark");
        afterUpdate.Item.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task update_cannot_resurrect_deleted_space()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Resurrection Attempt",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://resurrect.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Item!.IsDeleted.ShouldBeTrue();

        var config = getResult.Item;
        config.Name = "Renamed While Deleted";

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterUpdate.Item!.Name.ShouldBe("Renamed While Deleted");
        afterUpdate.Item.IsDeleted.ShouldBeTrue();
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
            _ct);

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second",
                MatchPatterns = [pattern]
            },
            _ct);

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
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task can_create_space_with_explicit_pool_id()
    {
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Manual Pool Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://manual-pool.example.com" }],
                PoolId = (PoolId)42
            },
            _ct);

        result.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.PoolId.Value.ShouldBe(42);
    }

    [Fact]
    public async Task create_with_explicit_pool_id_rejects_zero()
    {
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Zero Pool",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://zero-pool.example.com" }],
                PoolId = (PoolId)0
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_with_duplicate_pool_id_returns_error()
    {
        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "First Pool 99",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool99-first.example.com" }],
                PoolId = (PoolId)99
            },
            _ct);

        first.IsSuccess.ShouldBeTrue();

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second Pool 99",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool99-second.example.com" }],
                PoolId = (PoolId)99
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.Code == "validation_failed" &&
            e.PropertyNames != null &&
            e.Message.Contains("already in use", StringComparison.Ordinal) &&
            e.PropertyNames.Contains("PoolId"));
    }

    [Fact]
    public async Task can_purge_deleted_space()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "To Purge",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://purge.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);
        var purgeResult = await _admin.PurgeAsync(created.Id!, _ct);

        purgeResult.IsSuccess.ShouldBeTrue();

        // Space should not be retrievable after purge
        var retrieved = await _admin.GetAsync(created.Id!, _ct);
        retrieved.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task purge_rejects_non_deleted_space()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Not Deleted",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://not-deleted.example.com" }]
            },
            _ct);

        var result = await _admin.PurgeAsync(created.Id!, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task purge_is_idempotent_returns_not_found_on_second_call()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Double Purge",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://double-purge.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(created.Id!, _ct);
        await _admin.PurgeAsync(created.Id!, _ct);

        var secondPurge = await _admin.PurgeAsync(created.Id!, _ct);

        secondPurge.IsSuccess.ShouldBeFalse();
        secondPurge.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task purge_rejects_not_found()
    {
        var bogusId = (SpaceId)Guid.CreateVersion7();
        var result = await _admin.PurgeAsync(bogusId, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task purge_releases_match_patterns_for_reuse()
    {
        var pattern = new SpaceMatchPattern { Origin = "https://reuse-pattern.example.com" };

        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "First Owner",
                MatchPatterns = [pattern]
            },
            _ct);

        await _admin.DeleteAsync(first.Id!, _ct);
        await _admin.PurgeAsync(first.Id!, _ct);

        // Pattern should now be available for a new space
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second Owner",
                MatchPatterns = [pattern]
            },
            _ct);

        second.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task purge_releases_pool_id_for_reuse()
    {
        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Pool 50 Original",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool50-original.example.com" }],
                PoolId = (PoolId)50
            },
            _ct);

        await _admin.DeleteAsync(first.Id!, _ct);
        await _admin.PurgeAsync(first.Id!, _ct);

        // Pool ID 50 should now be available for a new space
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Pool 50 Reused",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool50-reused.example.com" }],
                PoolId = (PoolId)50
            },
            _ct);

        second.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task deleted_space_patterns_remain_reserved()
    {
        var pattern = new SpaceMatchPattern { Origin = "https://soft-delete-reuse.example.com" };

        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Original",
                MatchPatterns = [pattern]
            },
            _ct);

        await _admin.DeleteAsync(first.Id!, _ct);

        // Pattern should remain reserved after soft delete (only released on purge)
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Replacement",
                MatchPatterns = [pattern]
            },
            _ct);

        second.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task deleted_space_pool_id_remains_reserved()
    {
        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Pool 60 Original",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool60-original.example.com" }],
                PoolId = (PoolId)60
            },
            _ct);

        await _admin.DeleteAsync(first.Id!, _ct);

        // Pool ID should remain reserved after soft delete (only released on purge)
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Pool 60 Reused",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://pool60-reused.example.com" }],
                PoolId = (PoolId)60
            },
            _ct);

        second.IsSuccess.ShouldBeFalse();
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
            _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        var config = getResult.Item!;

        var tampered = new SpaceConfiguration
        {
            Id = config.Id,
            Name = config.Name,
            Enabled = config.Enabled,
            PoolId = config.PoolId.Value + 1,
            MatchPatterns = config.MatchPatterns
        };

        var result = await _admin.UpdateAsync(created.Id!, tampered, getResult.Version!, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_with_no_extended_properties_succeeds()
    {
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "No EAV Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://no-eav.example.com" }]
            },
            _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task create_with_extended_properties_but_no_schema_store_returns_error()
    {
        // Use an isolated service provider with no ISchemaStore registered
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        await using var services = sc.BuildServiceProvider();
        var storageInstanceSchema = services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);
        var admin = services.GetRequiredService<ISpaceAdmin>();

        var props = new AttributeValueCollection();
        props.Set(AttributeCode.Create("company_id"), "acme");

        var result = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "EAV No Schema",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://eav-no-schema.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_with_unknown_attribute_returns_validation_error()
    {
        var props = new AttributeValueCollection();
        props.Set(AttributeCode.Create("unknown_attr"), "value");

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Bad Attr Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://bad-attr.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_with_valid_extended_properties_round_trips_correctly()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "acme-corp");

        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://acme.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item!;
        loaded.ExtendedProperties.Count.ShouldBe(1);
        var attr = loaded.ExtendedProperties.OfType<AttributeValue<string>>()
            .FirstOrDefault(a => a.Code == CompanyIdDef.Code);
        attr.ShouldNotBeNull();
        attr.TypedValue.ShouldBe("acme-corp");
    }

    [Fact]
    public async Task create_with_all_scalar_types_round_trips_correctly()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "typed-co");
        props.Set(PriorityDef.Code, 42);
        props.Set(ActiveDef.Code, true);
        props.Set(RateDef.Code, 3.14m);
        props.Set(StartDateDef.Code, new DateOnly(2026, 1, 15));
        props.Set(CreatedAtDef.Code, new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));

        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "All Types Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://all-types.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(createResult.Id!, _ct);
        var loaded = getResult.Item!;
        loaded.ExtendedProperties.Count.ShouldBe(6);

        loaded.ExtendedProperties.OfType<AttributeValue<string>>()
            .First(a => a.Code == CompanyIdDef.Code).TypedValue.ShouldBe("typed-co");
        loaded.ExtendedProperties.OfType<AttributeValue<int>>()
            .First(a => a.Code == PriorityDef.Code).TypedValue.ShouldBe(42);
        loaded.ExtendedProperties.OfType<AttributeValue<bool>>()
            .First(a => a.Code == ActiveDef.Code).TypedValue.ShouldBeTrue();
        loaded.ExtendedProperties.OfType<AttributeValue<decimal>>()
            .First(a => a.Code == RateDef.Code).TypedValue.ShouldBe(3.14m);
        loaded.ExtendedProperties.OfType<AttributeValue<DateOnly>>()
            .First(a => a.Code == StartDateDef.Code).TypedValue.ShouldBe(new DateOnly(2026, 1, 15));
        loaded.ExtendedProperties.OfType<AttributeValue<DateTimeOffset>>()
            .First(a => a.Code == CreatedAtDef.Code).TypedValue.ShouldBe(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task unique_attribute_enforces_uniqueness_on_create()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "unique-company");

        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "First Unique",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://unique-first.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        first.IsSuccess.ShouldBeTrue();

        var duplicate = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second Unique",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://unique-second.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        duplicate.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task unique_attribute_enforces_uniqueness_on_update()
    {
        var propsA = new AttributeValueCollection();
        propsA.Set(CompanyIdDef.Code, "company-a");

        var propsB = new AttributeValueCollection();
        propsB.Set(CompanyIdDef.Code, "company-b");

        var spaceA = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space A",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://unique-update-a.example.com" }],
                ExtendedProperties = propsA
            },
            _ct);

        var spaceB = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://unique-update-b.example.com" }],
                ExtendedProperties = propsB
            },
            _ct);

        spaceA.IsSuccess.ShouldBeTrue();
        spaceB.IsSuccess.ShouldBeTrue();

        // Try to update space B to claim space A's unique company_id
        var getB = await _admin.GetAsync(spaceB.Id!, _ct);
        var updatedProps = new AttributeValueCollection();
        updatedProps.Set(CompanyIdDef.Code, "company-a");

        var updated = new SpaceConfiguration
        {
            Id = getB.Item!.Id,
            Name = getB.Item.Name,
            Enabled = getB.Item.Enabled,
            PoolId = getB.Item.PoolId,
            MatchPatterns = getB.Item.MatchPatterns,
            ExtendedProperties = updatedProps.ToList()
        };

        var result = await _admin.UpdateAsync(spaceB.Id!, updated, getB.Version!, _ct);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task update_preserves_and_modifies_extended_properties()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "original-company");

        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Update EAV Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update-eav.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(createResult.Id!, _ct);
        var config = getResult.Item!;

        var updatedProps = new AttributeValueCollection();
        updatedProps.Set(CompanyIdDef.Code, "updated-company");

        var updated = new SpaceConfiguration
        {
            Id = config.Id,
            Name = config.Name,
            Enabled = config.Enabled,
            PoolId = config.PoolId,
            MatchPatterns = config.MatchPatterns,
            ExtendedProperties = updatedProps.ToList()
        };

        var updateResult = await _admin.UpdateAsync(createResult.Id!, updated, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(createResult.Id!, _ct);
        var attr = afterUpdate.Item!.ExtendedProperties.OfType<AttributeValue<string>>()
            .FirstOrDefault(a => a.Code == CompanyIdDef.Code);
        attr.ShouldNotBeNull();
        attr.TypedValue.ShouldBe("updated-company");
    }

    [Fact]
    public async Task update_can_add_properties_to_space_that_had_none()
    {
        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "No Props Initially",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://add-props.example.com" }]
            },
            _ct);

        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(createResult.Id!, _ct);
        getResult.Item!.ExtendedProperties.Count.ShouldBe(0);

        var newProps = new AttributeValueCollection();
        newProps.Set(ThemeDef.Code, "dark");

        var updated = new SpaceConfiguration
        {
            Id = getResult.Item.Id,
            Name = getResult.Item.Name,
            Enabled = getResult.Item.Enabled,
            PoolId = getResult.Item.PoolId,
            MatchPatterns = getResult.Item.MatchPatterns,
            ExtendedProperties = newProps.ToList()
        };

        var updateResult = await _admin.UpdateAsync(createResult.Id!, updated, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Item!.ExtendedProperties.Count.ShouldBe(1);
        afterUpdate.Item.ExtendedProperties.OfType<AttributeValue<string>>()
            .First(a => a.Code == ThemeDef.Code).TypedValue.ShouldBe("dark");
    }

    [Fact]
    public async Task update_can_remove_all_properties_from_space()
    {
        var props = new AttributeValueCollection();
        props.Set(ThemeDef.Code, "light");

        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Remove Props Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://remove-props.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(createResult.Id!, _ct);
        getResult.Item!.ExtendedProperties.Count.ShouldBe(1);

        // Update with no extended properties
        var updated = new SpaceConfiguration
        {
            Id = getResult.Item.Id,
            Name = getResult.Item.Name,
            Enabled = getResult.Item.Enabled,
            PoolId = getResult.Item.PoolId,
            MatchPatterns = getResult.Item.MatchPatterns,
            ExtendedProperties = []
        };

        var updateResult = await _admin.UpdateAsync(createResult.Id!, updated, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Item!.ExtendedProperties.Count.ShouldBe(0);
    }

    [Fact]
    public async Task update_with_unknown_attribute_returns_validation_error()
    {
        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Update Bad Attr Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update-bad-attr.example.com" }]
            },
            _ct);

        var getResult = await _admin.GetAsync(createResult.Id!, _ct);

        var badProps = new AttributeValueCollection();
        badProps.Set(AttributeCode.Create("nonexistent"), "value");

        var updated = new SpaceConfiguration
        {
            Id = getResult.Item!.Id,
            Name = getResult.Item.Name,
            Enabled = getResult.Item.Enabled,
            PoolId = getResult.Item.PoolId,
            MatchPatterns = getResult.Item.MatchPatterns,
            ExtendedProperties = badProps.ToList()
        };

        var result = await _admin.UpdateAsync(createResult.Id!, updated, getResult.Version!, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task delete_drops_eav_unique_keys_so_value_can_be_reused()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "reusable-company");

        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Delete EAV Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://delete-eav.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        first.IsSuccess.ShouldBeTrue();
        await _admin.DeleteAsync(first.Id!, _ct);

        // After deletion, the same unique attribute value should be usable again
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Reuse EAV Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://reuse-eav.example.com" }],
                ExtendedProperties = props
            },
            _ct);

        second.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task update_on_deleted_space_does_not_reclaim_eav_unique_key_reused_by_another_space()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "reusable-after-update");

        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted With Unique Value",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update-after-delete-a.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        first.IsSuccess.ShouldBeTrue();

        await _admin.DeleteAsync(first.Id!, _ct);

        // Another space claims the value that A's deletion released.
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Reused Unique Value Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update-after-delete-b.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        second.IsSuccess.ShouldBeTrue();

        // Updating allowed (non-pattern) metadata on the deleted space A must succeed, and must not
        // re-register A's EAV unique key, which would otherwise conflict with B's claim.
        var getA = await _admin.GetAsync(first.Id!, _ct);
        getA.Found.ShouldBeTrue();
        var configA = getA.Item!;
        configA.Name = "Deleted A Renamed";

        var updateA = await _admin.UpdateAsync(first.Id!, configA, getA.Version!, _ct);
        updateA.IsSuccess.ShouldBeTrue();

        var afterUpdateA = await _admin.GetAsync(first.Id!, _ct);
        afterUpdateA.Item!.Name.ShouldBe("Deleted A Renamed");
        afterUpdateA.Item.IsDeleted.ShouldBeTrue();

        // B must still hold the unique value; a third space claiming the same value must be rejected.
        var third = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Should Conflict With B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://update-after-delete-c.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        third.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task update_cannot_change_match_pattern_of_deleted_space()
    {
        const string originalOrigin = "https://frozen-pattern.example.com";
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Frozen Pattern Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = originalOrigin }]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        await _admin.DeleteAsync(created.Id!, _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var config = getResult.Item!;
        config.MatchPatterns = [new SpaceMatchPattern { Origin = "https://new-pattern-attempt.example.com" }];

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldContain(e => e.Code == "validation_failed");

        // The original pattern must remain reserved (unchanged) after the rejected update.
        var afterFailedUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterFailedUpdate.Item!.MatchPatterns.ShouldContain(p => p.Origin == originalOrigin);
        afterFailedUpdate.Item.IsDeleted.ShouldBeTrue();

        // No other space may claim the frozen pattern while it is still reserved by the deleted space.
        var claimAttempt = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Attempt To Claim Frozen Pattern",
                MatchPatterns = [new SpaceMatchPattern { Origin = originalOrigin }]
            },
            _ct);
        claimAttempt.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task update_on_deleted_space_allows_metadata_changes_visible_via_admin_read_and_stays_deleted()
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted Admin Read Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://deleted-admin-read.example.com" }]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        await _admin.DeleteAsync(created.Id!, _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.IsDeleted.ShouldBeTrue();

        var newProps = new AttributeValueCollection();
        newProps.Set(ThemeDef.Code, "midnight");

        var config = getResult.Item;
        config.Name = "Deleted Admin Read Space Updated";
        config.Enabled = true;
        config.ExtendedProperties = newProps.ToList();

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.Name.ShouldBe("Deleted Admin Read Space Updated");
        afterUpdate.Item.ExtendedProperties.OfType<AttributeValue<string>>()
            .First(a => a.Code == ThemeDef.Code).TypedValue.ShouldBe("midnight");
        afterUpdate.Item.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task update_on_deleted_space_rejects_duplicate_pattern_substitution_that_would_release_reservation()
    {
        const string originA = "https://frozen-a.example.com";
        const string originB = "https://frozen-b.example.com";

        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Two Pattern Space",
                MatchPatterns =
                [
                    new SpaceMatchPattern { Origin = originA },
                    new SpaceMatchPattern { Origin = originB }
                ]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        await _admin.DeleteAsync(created.Id!, _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        // Attempt to substitute [A, B] with [A, A]. A naive order-independent "left values are all
        // present in right" comparison would incorrectly treat this as unchanged (both A's are in
        // {A, B}), which would let B's reservation lapse before the space is purged.
        var config = getResult.Item!;
        config.MatchPatterns =
        [
            new SpaceMatchPattern { Origin = originA },
            new SpaceMatchPattern { Origin = originA }
        ];

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();

        // B must remain reserved by the deleted space: another space may not claim it.
        var claimB = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Attempt To Claim B",
                MatchPatterns = [new SpaceMatchPattern { Origin = originB }]
            },
            _ct);
        claimB.IsSuccess.ShouldBeFalse();

        // The deleted space's patterns must remain unchanged.
        var afterFailedUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterFailedUpdate.Item!.MatchPatterns.Count.ShouldBe(2);
        afterFailedUpdate.Item.MatchPatterns.ShouldContain(p => p.Origin == originA);
        afterFailedUpdate.Item.MatchPatterns.ShouldContain(p => p.Origin == originB);
    }

    [Fact]
    public async Task create_rejects_duplicate_match_patterns()
    {
        const string origin = "https://duplicate-pattern.example.com";

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Duplicate Pattern Space",
                MatchPatterns =
                [
                    new SpaceMatchPattern { Origin = origin },
                    new SpaceMatchPattern { Origin = origin.ToUpperInvariant() }
                ]
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task update_on_deleted_space_accepts_reordered_but_otherwise_identical_patterns()
    {
        const string originA = "https://reorder-a.example.com";
        const string originB = "https://reorder-b.example.com";

        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Reorder Pattern Space",
                MatchPatterns =
                [
                    new SpaceMatchPattern { Origin = originA },
                    new SpaceMatchPattern { Origin = originB }
                ]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        await _admin.DeleteAsync(created.Id!, _ct);

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var config = getResult.Item!;
        config.MatchPatterns =
        [
            new SpaceMatchPattern { Origin = originB },
            new SpaceMatchPattern { Origin = originA }
        ];
        config.Name = "Reorder Pattern Space Renamed";

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterUpdate.Item!.Name.ShouldBe("Reorder Pattern Space Renamed");
        afterUpdate.Item.MatchPatterns.Count.ShouldBe(2);
        afterUpdate.Item.MatchPatterns.ShouldContain(p => p.Origin == originA);
        afterUpdate.Item.MatchPatterns.ShouldContain(p => p.Origin == originB);
    }

    // These tests exercise the TOCTOU gap directly at the repository layer, which is the sole
    // authority that closes it: SpaceAdmin.UpdateAsync reads the space once (an early, best-effort
    // check) and separately receives an expectedVersion from its caller; SpaceRepository.UpdateAsync
    // re-reads the row immediately before persisting. Reproducing the exact interleaving through the
    // public ISpaceAdmin API would require non-deterministic timing (or invasive hooks) to land a
    // concurrent DeleteAsync between SpaceAdmin's read and SpaceRepository's read while still
    // supplying an expectedVersion that matches the post-delete row. Calling SpaceRepository directly
    // reproduces the same interleaving deterministically: it presents a "stale" SpaceConfiguration
    // captured before a concurrent delete, together with the expectedVersion that is only valid
    // *after* that delete - exactly the shape of the race, without any timing dependency.
    [Fact]
    public async Task repository_update_rejects_pattern_change_when_expected_version_reflects_concurrent_delete()
    {
        const string originalOrigin = "https://race-frozen-pattern.example.com";
        const string attemptedOrigin = "https://race-new-pattern-attempt.example.com";

        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Race Frozen Pattern Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = originalOrigin }]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        var repository = _services.GetRequiredService<SpaceRepository>();

        // Simulates a caller that read the space (via GetByIdRawAsync, as SpaceAdmin.UpdateAsync
        // does) before the space was deleted. At this point IsDeleted is (correctly, at the time)
        // false.
        var staleRead = await repository.GetByIdRawAsync(created.Id!, _ct);
        staleRead.Found.ShouldBeTrue();
        var staleConfig = staleRead.Item!;
        staleConfig.IsDeleted.ShouldBeFalse();

        // A concurrent delete races in between the stale read above and the update below.
        await _admin.DeleteAsync(created.Id!, _ct);

        // The caller's expectedVersion reflects state observed *after* the concurrent delete
        // (e.g. from a separate version probe), while the SpaceConfiguration payload being applied
        // is still the stale, pre-delete snapshot that attempts to change the match pattern.
        var postDeleteRead = await repository.GetByIdRawAsync(created.Id!, _ct);
        postDeleteRead.Item!.IsDeleted.ShouldBeTrue();

        staleConfig.MatchPatterns = [new SpaceMatchPattern { Origin = attemptedOrigin }];

        var schemaStore = _services.GetRequiredService<ISchemaStore>();
        var schema = await schemaStore.GetAsync(SchemaId.Space, _ct);

        var result = await repository.UpdateAsync(
            staleConfig,
            new AttributeValueCollection(schema, staleConfig.ExtendedProperties),
            postDeleteRead.Version!.Value,
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed" &&
            e.PropertyNames != null && e.PropertyNames.Contains("MatchPatterns"));

        // The original pattern must remain reserved, and the attempted new pattern must remain free
        // to prove that the rejected update did not partially apply.
        var claimOriginal = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Attempt To Claim Original",
                MatchPatterns = [new SpaceMatchPattern { Origin = originalOrigin }]
            },
            _ct);
        claimOriginal.IsSuccess.ShouldBeFalse();

        var claimAttempted = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Attempt To Claim Attempted",
                MatchPatterns = [new SpaceMatchPattern { Origin = attemptedOrigin }]
            },
            _ct);
        claimAttempted.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task repository_update_does_not_reclaim_eav_unique_key_when_expected_version_reflects_concurrent_delete()
    {
        var props = new AttributeValueCollection();
        props.Set(CompanyIdDef.Code, "race-reusable-company");

        var first = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Race Deleted With Unique Value",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://race-update-after-delete-a.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        first.IsSuccess.ShouldBeTrue();

        var repository = _services.GetRequiredService<SpaceRepository>();

        // Stale read captured before the concurrent delete: still holds the unique attribute value
        // and IsDeleted = false.
        var staleRead = await repository.GetByIdRawAsync(first.Id!, _ct);
        staleRead.Found.ShouldBeTrue();
        var staleConfig = staleRead.Item!;
        staleConfig.IsDeleted.ShouldBeFalse();

        // Concurrent delete releases the unique key.
        await _admin.DeleteAsync(first.Id!, _ct);

        // Another space claims the value that A's deletion released.
        var second = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Race Reused Unique Value Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://race-update-after-delete-b.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        second.IsSuccess.ShouldBeTrue();

        var postDeleteRead = await repository.GetByIdRawAsync(first.Id!, _ct);
        postDeleteRead.Item!.IsDeleted.ShouldBeTrue();

        // Apply a non-pattern metadata edit using the stale (pre-delete) payload, whose
        // ExtendedProperties still carry the now-reclaimed-by-B unique value, but with the
        // expectedVersion that only matches the row *after* the concurrent delete. If EAV
        // suppression were derived from a stale caller-supplied IsDeleted flag rather than the
        // freshly re-read row, this would attempt to rebuild A's unique key and collide with B.
        staleConfig.Name = "Race Deleted A Renamed";

        var schemaStore = _services.GetRequiredService<ISchemaStore>();
        var schema = await schemaStore.GetAsync(SchemaId.Space, _ct);

        var updateResult = await repository.UpdateAsync(
            staleConfig,
            new AttributeValueCollection(schema, staleConfig.ExtendedProperties),
            postDeleteRead.Version!.Value,
            _ct);

        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(first.Id!, _ct);
        afterUpdate.Item!.Name.ShouldBe("Race Deleted A Renamed");
        afterUpdate.Item.IsDeleted.ShouldBeTrue();

        // B must still hold the unique value; a third space claiming the same value must be rejected.
        var third = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Race Should Conflict With B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://race-update-after-delete-c.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        third.IsSuccess.ShouldBeFalse();
    }

    // Exercises SpaceRepository.UpdateAsync directly (not via ISpaceAdmin) so the assertion is
    // about the repository's own authoritative check, independent of SpaceAdmin's best-effort
    // pre-check (which would otherwise reject the same request first and hide whether the
    // repository's own guard is correct). Persists [A, B] on a deleted space, then attempts to
    // substitute [A, A] with a correct (non-stale) expectedVersion: the repository must reject
    // the substitution on its own, and B must remain reserved.
    [Fact]
    public async Task repository_update_rejects_duplicate_pattern_substitution_on_persisted_deleted_space()
    {
        const string originA = "https://repo-frozen-a.example.com";
        const string originB = "https://repo-frozen-b.example.com";

        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Repo Two Pattern Space",
                MatchPatterns =
                [
                    new SpaceMatchPattern { Origin = originA },
                    new SpaceMatchPattern { Origin = originB }
                ]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        await _admin.DeleteAsync(created.Id!, _ct);

        var repository = _services.GetRequiredService<SpaceRepository>();
        var current = await repository.GetByIdRawAsync(created.Id!, _ct);
        current.Found.ShouldBeTrue();
        current.Item!.IsDeleted.ShouldBeTrue();
        current.Item.MatchPatterns.Count.ShouldBe(2);

        // Substitute [A, B] with [A, A] using the correct, up-to-date expectedVersion. A naive
        // "left values are all present in right" comparison would treat this as unchanged (both
        // A's are present in {A, B}), silently releasing B's reservation.
        var config = current.Item;
        config.MatchPatterns =
        [
            new SpaceMatchPattern { Origin = originA },
            new SpaceMatchPattern { Origin = originA }
        ];

        var schemaStore = _services.GetRequiredService<ISchemaStore>();
        var schema = await schemaStore.GetAsync(SchemaId.Space, _ct);

        var updateResult = await repository.UpdateAsync(
            config,
            new AttributeValueCollection(schema, config.ExtendedProperties),
            current.Version!.Value,
            _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldContain(e => e.Code == "validation_failed" &&
            e.PropertyNames != null && e.PropertyNames.Contains("MatchPatterns"));

        // B must remain reserved by the deleted space: no other space may claim it.
        var claimB = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Repo Attempt To Claim B",
                MatchPatterns = [new SpaceMatchPattern { Origin = originB }]
            },
            _ct);
        claimB.IsSuccess.ShouldBeFalse();

        // The persisted patterns must remain unchanged.
        var afterFailedUpdate = await repository.GetByIdRawAsync(created.Id!, _ct);
        afterFailedUpdate.Item!.MatchPatterns.Count.ShouldBe(2);
        afterFailedUpdate.Item.MatchPatterns.ShouldContain(p => p.Origin == originA);
        afterFailedUpdate.Item.MatchPatterns.ShouldContain(p => p.Origin == originB);
    }

    // Queries the underlying storage's search fields directly (bypassing ISpaceAdmin.QueryAsync,
    // which currently filters client-side on the DSO and does not exercise search fields at all)
    // to make the EAV search-field lifecycle deterministic and behavior-focused: proves the
    // queryable "theme" search field is present while the space is live, removed once the space is
    // deleted, and stays removed after a normal deleted-space metadata update (via ISpaceAdmin).
    [Fact]
    public async Task deleted_space_eav_search_field_is_removed_and_stays_removed_after_normal_update()
    {
        const string themeValue = "search-field-lifecycle-theme";
        var themeField = new StringField("attr:theme").Equals(themeValue);

        var storageAccessor = _services.GetRequiredService<ManagementStorageAccessor>();
        var partitionedStorage = storageAccessor.GetManagementStorage();

        async Task<bool> ThemeSearchFieldMatchesAsync()
        {
            var result = await partitionedStorage.QueryAsync<SpaceDso.V1>(
                SpaceDso.EntityType,
                themeField,
                SortParameter.Empty,
                DataRange.FromOffset(null, null),
                _ct);
            return result.Items.Count > 0;
        }

        var props = new AttributeValueCollection();
        props.Set(ThemeDef.Code, themeValue);

        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Search Field Lifecycle Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://search-field-lifecycle.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        // Search field is present while the space is live.
        (await ThemeSearchFieldMatchesAsync()).ShouldBeTrue();

        await _admin.DeleteAsync(created.Id!, _ct);

        // Deletion removes the search field.
        (await ThemeSearchFieldMatchesAsync()).ShouldBeFalse();

        // A normal deleted-space metadata update (via ISpaceAdmin) must not resurrect the search field.
        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        var config = getResult.Item!;
        config.Name = "Search Field Lifecycle Space Renamed";

        var updateResult = await _admin.UpdateAsync(created.Id!, config, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        (await ThemeSearchFieldMatchesAsync()).ShouldBeFalse();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.Name.ShouldBe("Search Field Lifecycle Space Renamed");
        afterUpdate.Item.IsDeleted.ShouldBeTrue();
    }

    // Reproduces the same deterministic race as the "repository_update_*" tests above (see the
    // comment preceding repository_update_rejects_pattern_change_when_expected_version_reflects_concurrent_delete):
    // the stale SpaceConfiguration must be captured *before* the concurrent delete, while the space
    // is still live, so its IsDeleted flag genuinely reflects the pre-delete state. The subsequent
    // repository-level update is then applied using that pre-delete payload together with the
    // expectedVersion observed *after* the delete - the exact shape of the race. This proves that
    // EAV search-field suppression is derived from the freshly re-read row's deletion state, not
    // from a stale caller-supplied IsDeleted flag, even though the stale payload's
    // ExtendedProperties still carry the theme value.
    [Fact]
    public async Task deleted_space_eav_search_field_stays_removed_after_stale_concurrent_delete_update()
    {
        const string themeValue = "search-field-lifecycle-theme-raced";
        var themeField = new StringField("attr:theme").Equals(themeValue);

        var storageAccessor = _services.GetRequiredService<ManagementStorageAccessor>();
        var partitionedStorage = storageAccessor.GetManagementStorage();

        async Task<bool> ThemeSearchFieldMatchesAsync()
        {
            var result = await partitionedStorage.QueryAsync<SpaceDso.V1>(
                SpaceDso.EntityType,
                themeField,
                SortParameter.Empty,
                DataRange.FromOffset(null, null),
                _ct);
            return result.Items.Count > 0;
        }

        var props = new AttributeValueCollection();
        props.Set(ThemeDef.Code, themeValue);

        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Search Field Lifecycle Space Raced Source",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://search-field-lifecycle-raced.example.com" }],
                ExtendedProperties = props
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        // Search field is present while the space is live.
        (await ThemeSearchFieldMatchesAsync()).ShouldBeTrue();

        var repository = _services.GetRequiredService<SpaceRepository>();

        // Simulates a caller that read the space (via GetByIdRawAsync, as SpaceAdmin.UpdateAsync
        // does) before the space was deleted. At this point IsDeleted is (correctly, at the time)
        // false, and the payload's ExtendedProperties still carry the theme value.
        var staleRead = await repository.GetByIdRawAsync(created.Id!, _ct);
        staleRead.Found.ShouldBeTrue();
        var staleConfig = staleRead.Item!;
        staleConfig.IsDeleted.ShouldBeFalse();

        // A concurrent delete races in between the stale read above and the update below.
        await _admin.DeleteAsync(created.Id!, _ct);

        // Deletion removes the search field.
        (await ThemeSearchFieldMatchesAsync()).ShouldBeFalse();

        // The caller's expectedVersion reflects state observed *after* the concurrent delete, while
        // the SpaceConfiguration payload being applied is still the stale, pre-delete snapshot.
        var postDeleteRead = await repository.GetByIdRawAsync(created.Id!, _ct);
        postDeleteRead.Item!.IsDeleted.ShouldBeTrue();

        staleConfig.Name = "Search Field Lifecycle Space Raced";

        var schemaStore = _services.GetRequiredService<ISchemaStore>();
        var schema = await schemaStore.GetAsync(SchemaId.Space, _ct);

        var repoUpdateResult = await repository.UpdateAsync(
            staleConfig,
            new AttributeValueCollection(schema, staleConfig.ExtendedProperties),
            postDeleteRead.Version!.Value,
            _ct);

        repoUpdateResult.IsSuccess.ShouldBeTrue();

        // Even though staleConfig's ExtendedProperties still carry the theme value, the search
        // field must not be resurrected because the row is deleted at the time of the update.
        (await ThemeSearchFieldMatchesAsync()).ShouldBeFalse();

        var afterRepoUpdate = await _admin.GetAsync(created.Id!, _ct);
        afterRepoUpdate.Found.ShouldBeTrue();
        afterRepoUpdate.Item!.Name.ShouldBe("Search Field Lifecycle Space Raced");
        afterRepoUpdate.Item.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task undelete_restores_soft_deleted_space()
    {
        const string spaceOrigin = "https://undelete.example.com";
        const string spaceName = "Undelete Space";
        var spacePattern = new SpaceMatchPattern { Origin = spaceOrigin };

        var createSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = spaceName,
                MatchPatterns = [spacePattern]
            },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue();

        var spaceId = createSpaceResult.Id!;
        var spaceBeforeDelete = await _admin.GetAsync(spaceId, _ct);
        var autoAssignedPoolId = spaceBeforeDelete.Item!.PoolId;

        await _admin.DeleteAsync(spaceId, _ct);

        var undeleteResult = await _admin.UndeleteAsync(spaceId, _ct);
        undeleteResult.IsSuccess.ShouldBeTrue();

        var restoredSpace = await _admin.GetAsync(spaceId, _ct);
        restoredSpace.Found.ShouldBeTrue("restored space must be findable by id");
        restoredSpace.Item!.Name.ShouldBe(spaceName, "name must survive the round trip");
        restoredSpace.Item.PoolId.ShouldBe(autoAssignedPoolId, "auto-assigned pool id must survive the round trip");
        restoredSpace.Item.MatchPatterns.ShouldContain(
            pattern => pattern.Origin == spaceOrigin,
            "match patterns must survive the round trip");

        var allSpaces = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(),
            _ct);
        allSpaces.Items.ShouldContain(
            space => space.Id == spaceId.Value,
            "restored space must reappear in QueryAsync results");

        var store = _services.GetRequiredService<ISpaceStore>();

        var resolvedSpace = await store.TryResolveSpace(spacePattern, _ct);
        resolvedSpace.ShouldNotBeNull("restored space must resolve for live traffic");
        resolvedSpace.SpaceId.ShouldBe(
            spaceId.Value,
            "resolution must return the restored space id, not some other space");

        var isOriginClaimed = await store.IsOriginClaimed(spaceOrigin, _ct);
        isOriginClaimed.ShouldBeTrue("origin must be reported as claimed once the space is restored");
    }

    [Fact]
    public async Task restored_space_serves_traffic_that_was_rejected_while_deleted()
    {
        const string spaceOrigin = "https://cache-bust.example.com";
        var spacePattern = new SpaceMatchPattern { Origin = spaceOrigin };

        var createSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Cache Bust Space",
                MatchPatterns = [spacePattern]
            },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue();

        var spaceId = createSpaceResult.Id!;
        var store = _services.GetRequiredService<ISpaceStore>();

        var resolvedBeforeDelete = await store.TryResolveSpace(spacePattern, _ct);
        resolvedBeforeDelete.ShouldNotBeNull("baseline: created space must resolve, and this warms the positive cache entries");
        (await store.IsOriginClaimed(spaceOrigin, _ct)).ShouldBeTrue("baseline: origin must be claimed, and this warms the origin-claim cache");

        await _admin.DeleteAsync(spaceId, _ct);

        var resolvedAfterDelete = await store.TryResolveSpace(spacePattern, _ct);
        resolvedAfterDelete.ShouldBeNull("delete must bust the pattern cache: a stale positive would keep routing traffic to the deleted space");
        (await store.IsOriginClaimed(spaceOrigin, _ct)).ShouldBeFalse("delete must bust the origin-claim cache: this call also populates a poisoned negative entry");

        await _admin.UndeleteAsync(spaceId, _ct);

        var resolvedAfterUndelete = await store.TryResolveSpace(spacePattern, _ct);
        resolvedAfterUndelete.ShouldNotBeNull("undelete must evict the poisoned negative pattern-cache entry");
        resolvedAfterUndelete.SpaceId.ShouldBe(spaceId.Value, "resolution must return the restored space, not a stale null");

        (await store.IsOriginClaimed(spaceOrigin, _ct)).ShouldBeTrue("undelete must evict the poisoned negative origin-claim cache entry");
    }

    [Fact]
    public async Task undelete_never_existed_returns_not_found()
    {
        var neverExistedSpaceId = (SpaceId)Guid.CreateVersion7();

        var undeleteResult = await _admin.UndeleteAsync(neverExistedSpaceId, _ct);

        undeleteResult.IsSuccess.ShouldBeFalse();
        undeleteResult.Errors.ShouldContain(
            error => error.Code == "not_found",
            "failure must be reported as not_found so callers can distinguish it from other errors");
    }

    [Fact]
    public async Task undelete_after_purge_returns_not_found()
    {
        var createSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Purge Then Undelete",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://purge-undelete.example.com" }]
            },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue();

        var purgedSpaceId = createSpaceResult.Id!;
        await _admin.DeleteAsync(purgedSpaceId, _ct);
        await _admin.PurgeAsync(purgedSpaceId, _ct);

        var undeleteResult = await _admin.UndeleteAsync(purgedSpaceId, _ct);

        undeleteResult.IsSuccess.ShouldBeFalse();
        undeleteResult.Errors.ShouldContain(
            error => error.Code == "not_found",
            "purged space must be indistinguishable from a never-existed space");
    }

    [Fact]
    public async Task undelete_fails_when_a_unique_value_is_taken_by_another_space()
    {
        const string contestedCompanyId = "claimed-value";

        var spaceProperties = new AttributeValueCollection();
        spaceProperties.Set(CompanyIdDef.Code, contestedCompanyId);

        var createDeletedSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://a.example.com" }],
                ExtendedProperties = spaceProperties
            },
            _ct);
        createDeletedSpaceResult.IsSuccess.ShouldBeTrue();

        var deletedSpaceId = createDeletedSpaceResult.Id!;
        await _admin.DeleteAsync(deletedSpaceId, _ct);

        var createClaimantSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Claimant Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://b.example.com" }],
                ExtendedProperties = spaceProperties
            },
            _ct);
        createClaimantSpaceResult.IsSuccess.ShouldBeTrue(
            "claimant space must be creatable while the original is soft-deleted (delete drops unique keys)");

        var claimantSpaceId = createClaimantSpaceResult.Id!;

        var undeleteResult = await _admin.UndeleteAsync(deletedSpaceId, _ct);

        undeleteResult.IsSuccess.ShouldBeFalse(
            "undelete must fail when a remembered unique EAV value has been claimed by another space");
        undeleteResult.Errors.ShouldContain(
            error =>
                error.Code == "eav_conflict" &&
                error.Message.Contains(CompanyIdDef.Code.Value, StringComparison.Ordinal) &&
                error.Message.Contains(contestedCompanyId, StringComparison.Ordinal) &&
                error.Message.Contains(claimantSpaceId.Value.ToString(), StringComparison.Ordinal),
            "conflict error must name the attribute, the contested value, and the current holder so operators can resolve it");

        var deletedSpaceAfterFailedUndelete = await _admin.GetAsync(deletedSpaceId, _ct);
        deletedSpaceAfterFailedUndelete.Found.ShouldBeTrue();
        deletedSpaceAfterFailedUndelete.Item!.IsDeleted.ShouldBeTrue(
            "failed undelete must be atomic: the space remains soft-deleted");
    }

    [Fact]
    public async Task undelete_fails_and_reports_every_conflicting_attribute()
    {
        const string contestedCompanyId = "conflict-x";
        const string contestedSecondaryId = "conflict-y";
        const string uncontestedTertiaryId = "conflict-z";

        var deletedSpaceProperties = new AttributeValueCollection();
        deletedSpaceProperties.Set(CompanyIdDef.Code, contestedCompanyId);
        deletedSpaceProperties.Set(SecondaryIdDef.Code, contestedSecondaryId);
        deletedSpaceProperties.Set(TertiaryIdDef.Code, uncontestedTertiaryId);

        var createDeletedSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted Space With Three Unique Values",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://multi-a.example.com" }],
                ExtendedProperties = deletedSpaceProperties
            },
            _ct);
        createDeletedSpaceResult.IsSuccess.ShouldBeTrue();

        var deletedSpaceId = createDeletedSpaceResult.Id!;
        await _admin.DeleteAsync(deletedSpaceId, _ct);

        var companyIdClaimantProperties = new AttributeValueCollection();
        companyIdClaimantProperties.Set(CompanyIdDef.Code, contestedCompanyId);

        var createCompanyIdClaimantResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Company Id Claimant",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://multi-b.example.com" }],
                ExtendedProperties = companyIdClaimantProperties
            },
            _ct);
        createCompanyIdClaimantResult.IsSuccess.ShouldBeTrue("company-id claimant creation is a precondition for this test");
        var companyIdClaimantSpaceId = createCompanyIdClaimantResult.Id!;

        var secondaryIdClaimantProperties = new AttributeValueCollection();
        secondaryIdClaimantProperties.Set(SecondaryIdDef.Code, contestedSecondaryId);

        var createSecondaryIdClaimantResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Secondary Id Claimant",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://multi-c.example.com" }],
                ExtendedProperties = secondaryIdClaimantProperties
            },
            _ct);
        createSecondaryIdClaimantResult.IsSuccess.ShouldBeTrue("secondary-id claimant creation is a precondition for this test");
        var secondaryIdClaimantSpaceId = createSecondaryIdClaimantResult.Id!;

        var undeleteResult = await _admin.UndeleteAsync(deletedSpaceId, _ct);

        undeleteResult.IsSuccess.ShouldBeFalse("undelete must fail when any remembered unique EAV value is claimed");
        undeleteResult.Errors.ShouldContain(
            error =>
                error.Code == "eav_conflict" &&
                error.Message.Contains(CompanyIdDef.Code.Value, StringComparison.Ordinal) &&
                error.Message.Contains(contestedCompanyId, StringComparison.Ordinal) &&
                error.Message.Contains(companyIdClaimantSpaceId.Value.ToString(), StringComparison.Ordinal),
            "first conflict (company-id) must be enumerated with attribute, value, and holder");
        undeleteResult.Errors.ShouldContain(
            error =>
                error.Code == "eav_conflict" &&
                error.Message.Contains(SecondaryIdDef.Code.Value, StringComparison.Ordinal) &&
                error.Message.Contains(contestedSecondaryId, StringComparison.Ordinal) &&
                error.Message.Contains(secondaryIdClaimantSpaceId.Value.ToString(), StringComparison.Ordinal),
            "second conflict (secondary-id) must also be enumerated: enumeration is exhaustive, not fail-fast");
        undeleteResult.Errors.ShouldNotContain(
            error => error.Message.Contains(uncontestedTertiaryId, StringComparison.Ordinal),
            "uncontested attribute must not appear in the conflict list");

        var deletedSpaceAfterFailedUndelete = await _admin.GetAsync(deletedSpaceId, _ct);
        deletedSpaceAfterFailedUndelete.Found.ShouldBeTrue();
        deletedSpaceAfterFailedUndelete.Item!.IsDeleted.ShouldBeTrue(
            "failed undelete must be atomic: the space remains soft-deleted");
    }

    [Fact]
    public async Task undelete_prevents_another_space_from_claiming_the_restored_values()
    {
        const string reclaimedCompanyId = "reclaimed-value";

        var deletedSpaceProperties = new AttributeValueCollection();
        deletedSpaceProperties.Set(CompanyIdDef.Code, reclaimedCompanyId);

        var createDeletedSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Reclaim Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://reclaim.example.com" }],
                ExtendedProperties = deletedSpaceProperties
            },
            _ct);
        createDeletedSpaceResult.IsSuccess.ShouldBeTrue();

        var reclaimedSpaceId = createDeletedSpaceResult.Id!;
        await _admin.DeleteAsync(reclaimedSpaceId, _ct);

        var undeleteResult = await _admin.UndeleteAsync(reclaimedSpaceId, _ct);
        undeleteResult.IsSuccess.ShouldBeTrue();

        var wouldCollideProperties = new AttributeValueCollection();
        wouldCollideProperties.Set(CompanyIdDef.Code, reclaimedCompanyId);

        var createWouldCollideResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Would Collide Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://collide.example.com" }],
                ExtendedProperties = wouldCollideProperties
            },
            _ct);

        createWouldCollideResult.IsSuccess.ShouldBeFalse(
            "the unique EAV key must be re-registered on undelete, so a second space cannot claim the same value");
    }

    [Fact]
    public async Task undelete_round_trip_bumps_version_only_on_real_state_change()
    {
        var createSpaceResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Round Trip Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://round-trip.example.com" }]
            },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue();

        var spaceId = createSpaceResult.Id!;
        var versionAfterCreate = createSpaceResult.Version!.Value;

        var firstDeleteResult = await _admin.DeleteAsync(spaceId, _ct);
        var versionAfterFirstDelete = firstDeleteResult.Version!.Value;
        versionAfterFirstDelete.ShouldBeGreaterThan(
            versionAfterCreate,
            "delete is a state change and must bump the version");

        var firstUndeleteResult = await _admin.UndeleteAsync(spaceId, _ct);
        var versionAfterFirstUndelete = firstUndeleteResult.Version!.Value;
        versionAfterFirstUndelete.ShouldBeGreaterThan(
            versionAfterFirstDelete,
            "undelete of a soft-deleted space is a state change and must bump the version");

        var noopUndeleteResult = await _admin.UndeleteAsync(spaceId, _ct);
        var versionAfterNoopUndelete = noopUndeleteResult.Version!.Value;
        versionAfterNoopUndelete.ShouldBe(
            versionAfterFirstUndelete,
            "undelete of an already-active space is a no-op and must not bump the version");

        var secondDeleteResult = await _admin.DeleteAsync(spaceId, _ct);
        var versionAfterSecondDelete = secondDeleteResult.Version!.Value;
        versionAfterSecondDelete.ShouldBeGreaterThan(
            versionAfterFirstUndelete,
            "a second delete after restore must bump the version again");

        var secondUndeleteResult = await _admin.UndeleteAsync(spaceId, _ct);
        var versionAfterSecondUndelete = secondUndeleteResult.Version!.Value;
        versionAfterSecondUndelete.ShouldBeGreaterThan(
            versionAfterSecondDelete,
            "a second undelete after re-delete must bump the version again");
    }

    [Fact]
    public async Task undelete_fails_with_schema_drift_when_stored_attribute_missing_from_schema()
    {
        // Two service providers share the same in-memory SQLite database so that
        // the same physical space record can be seen under two different schemas.
        var sharedDatabase = $"schema-drift-{Guid.NewGuid():N}";

        var schemaWithBothAttributes = new SchemaConfiguration
        {
            SchemaId = SchemaId.Space,
            DisplayName = "Space",
            Description = "Extended attributes for spaces.",
            AttributeDefinitions = [CompanyIdDef, SecondaryIdDef]
        };

        var originalSchemaCollection = new ServiceCollection()
            .AddLogging()
            .AddSpaces()
            .AddStorageInternal(builder => builder.AddSqliteInMemory(dataSourceName: sharedDatabase))
            .AddSingleton<ISchemaStore>(new InMemorySchemaStore([schemaWithBothAttributes]));
        TestSpacesLicense.RegisterEntitled(originalSchemaCollection);
        await using var originalSchemaServices = originalSchemaCollection.BuildServiceProvider();

        await originalSchemaServices.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(_ct);
        var adminUnderOriginalSchema = originalSchemaServices.GetRequiredService<ISpaceAdmin>();

        var driftedSpaceProperties = new AttributeValueCollection();
        driftedSpaceProperties.Set(CompanyIdDef.Code, "drift-corp");

        var createDriftedSpaceResult = await adminUnderOriginalSchema.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Schema Drift Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://schema-drift.example.com" }],
                ExtendedProperties = driftedSpaceProperties
            },
            _ct);
        createDriftedSpaceResult.IsSuccess.ShouldBeTrue("space creation is a precondition for this test");

        var driftedSpaceId = createDriftedSpaceResult.Id!;
        await adminUnderOriginalSchema.DeleteAsync(driftedSpaceId, _ct);

        var schemaMissingCompanyId = new SchemaConfiguration
        {
            SchemaId = SchemaId.Space,
            DisplayName = "Space",
            Description = "Extended attributes for spaces.",
            AttributeDefinitions = [SecondaryIdDef]
        };

        var reducedSchemaCollection = new ServiceCollection()
            .AddLogging()
            .AddSpaces()
            .AddStorageInternal(builder => builder.AddSqliteInMemory(dataSourceName: sharedDatabase))
            .AddSingleton<ISchemaStore>(new InMemorySchemaStore([schemaMissingCompanyId]));
        TestSpacesLicense.RegisterEntitled(reducedSchemaCollection);
        await using var reducedSchemaServices = reducedSchemaCollection.BuildServiceProvider();

        var adminUnderReducedSchema = reducedSchemaServices.GetRequiredService<ISpaceAdmin>();

        var undeleteResult = await adminUnderReducedSchema.UndeleteAsync(driftedSpaceId, _ct);

        undeleteResult.IsSuccess.ShouldBeFalse(
            "undelete must fail when the current schema cannot represent a stored attribute (silent drop would lose data)");
        undeleteResult.Errors.ShouldContain(
            error =>
                error.Code == "schema_drift" &&
                error.Message.Contains(CompanyIdDef.Code.Value, StringComparison.Ordinal),
            "drift error must name the specific attribute the current schema cannot represent");

        var driftedSpaceAfterFailedUndelete = await adminUnderReducedSchema.GetAsync(driftedSpaceId, _ct);
        driftedSpaceAfterFailedUndelete.Found.ShouldBeTrue();
        driftedSpaceAfterFailedUndelete.Item!.IsDeleted.ShouldBeTrue(
            "failed undelete must be atomic: the space remains soft-deleted");
    }
}
