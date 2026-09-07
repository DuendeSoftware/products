// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
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

    private static readonly SchemaConfiguration SpaceSchema = new()
    {
        SchemaId = SchemaId.Space,
        DisplayName = "Space",
        Description = "Extended attributes for spaces.",
        AttributeDefinitions = [CompanyIdDef, ThemeDef, PriorityDef, ActiveDef, RateDef, StartDateDef, CreatedAtDef]
    };

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        sc.AddStorageInternal(b => b.AddSqliteInMemoryStore());
        sc.AddSingleton<ISchemaStore>(new InMemorySchemaStore([SpaceSchema]));
        _services = sc.BuildServiceProvider();

        var schema = _services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

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
    public async Task query_excludes_deleted_spaces()
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

        all.Items.ShouldNotContain(s => s.Name == "Will Be Deleted");
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
        sc.AddStorageInternal(b => b.AddSqliteInMemoryStore());
        await using var services = sc.BuildServiceProvider();
        var dbSchema = services.GetRequiredService<IDatabaseSchema>();
        await dbSchema.MigrateAsync(_ct);
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
}
