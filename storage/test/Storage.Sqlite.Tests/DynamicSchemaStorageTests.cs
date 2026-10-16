// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.Sqlite;

public sealed class DynamicSchemaStorageTests
{
    private static SchemaConfiguration ValidSchema(string id) => new()
    {
        SchemaId = SchemaId.Create(id),
        Groups = [new AttributeGroup(AttributeGroupCode.Create("general"), AttributeDisplayName.Create("General"), null, Order: 0)],
        AttributeDefinitions =
        [
            new AttributeDefinition
            {
                Code = AttributeCode.Create("nickname"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
            }
        ],
    };

    private static Task<IStorageFixture> CreateFixtureAsync(Ct ct) =>
        new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services =>
        {
            services.AddDynamicSchemaStorage();
            _ = services.AddFakeLogging();
        });

    [Fact]
    public async Task Query_returns_the_schema_id_and_display_name_of_every_saved_schema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();

        var first = ValidSchema("tenant-a:Profile");
        first.DisplayName = "Tenant profile";
        var second = ValidSchema("device");
        var third = ValidSchema("Api-Scope");
        third.DisplayName = "API scope";
        third.Groups.Add(new AttributeGroup(AttributeGroupCode.Create("extra"), null, null, Order: 1));
        third.AttributeDefinitions.Add(new AttributeDefinition
        {
            Code = AttributeCode.Create("scope_name"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
        });
        var schemas = new[] { first, second, third };

        foreach (var schema in schemas)
        {
            (await admin.CreateAsync(schema, ct)).IsSuccess.ShouldBeTrue();
        }

        var result = await admin.QueryAsync(ct);

        var expected = schemas
            .Select(s => (s.SchemaId.Value, s.DisplayName, s.AttributeDefinitions.Count, s.Groups.Count))
            .ToList();
        result.Items
            .Select(i => (i.SchemaId.Value, i.DisplayName, i.AttributeCount, i.GroupCount))
            .ToList()
            .ShouldBe(expected, ignoreOrder: true);
        result.TotalCount.ShouldBe(3);
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_returns_the_display_name_and_description_the_schema_was_created_with()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();

        var schema = ValidSchema("Customer-Profile");
        schema.DisplayName = "Customer profile";
        schema.Description = "Attributes collected at sign-up";
        (await admin.CreateAsync(schema, ct)).IsSuccess.ShouldBeTrue();
        var requested = SchemaId.Create("customer-profile");

        var created = await admin.GetAsync(requested, ct);

        created.Found.ShouldBeTrue();
        created.Item!.DisplayName.ShouldBe(schema.DisplayName);
        created.Item.Description.ShouldBe(schema.Description);
        created.Item.SchemaId.Value.ShouldBe(schema.SchemaId.Value);

        var changed = ValidSchema("Customer-Profile");
        changed.DisplayName = "Customer profile (renamed)";
        changed.Description = schema.Description;
        (await admin.UpdateAsync(changed.SchemaId, changed, created.Version!.Value, ct)).IsSuccess.ShouldBeTrue();

        var updated = await admin.GetAsync(requested, ct);

        updated.Found.ShouldBeTrue();
        updated.Item!.DisplayName.ShouldBe(changed.DisplayName);
        updated.Item.Description.ShouldBe(changed.Description);
        updated.Item.SchemaId.Value.ShouldBe(changed.SchemaId.Value);
    }

    private static readonly UuidV7 UserManagement1ProfileSchemaRowId =
        UuidV7.From(Guid.Parse("01968712-737f-70bf-a185-2da272266bd4"));

    private static AttributeSchemaDso.V1 DsoWithoutSchemaId() =>
        new(null, null, null,
        [
            new AttributeDefinitionDso.V1(
                "nickname", new AttributeTypeDso("Scalar", "String", null, null, null, null),
                null, false, [], null, 0, null, false, false)
        ], []);

    private static async Task SeedAsync(IStorageFixture fixture, UuidV7 rowId, DataStorageKey[] keys, Ct ct)
    {
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, ct);
        var seeded = await storage.CreateAsync(rowId, DsoWithoutSchemaId(), keys, SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        seeded.ShouldBe(CreateResult.Success);
    }

    private static async Task<AttributeSchemaDso.V1> ReadStoredDsoAsync(IStorageFixture fixture, UuidV7 rowId, Ct ct)
    {
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, ct);
        var stored = await storage.TryReadAsync(AttributeSchemaDso.EntityType, rowId, ct);
        stored.Found.ShouldBeTrue();
        return stored.Dso.ShouldBeOfType<AttributeSchemaDso.V1>();
    }

    [Fact]
    public async Task A_pre_release_schema_is_reported_as_unknown_until_it_is_read_by_id_and_upconverted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        var rowId = UuidV7.New();

        // Pre-release builds saved the schema with its key, but without the schema id in the document.
        await SeedAsync(fixture, rowId, [DataStorageKey.Create(SchemaIdDskV1.Create(SchemaId.Create("pre-release")))], ct);

        (await admin.QueryAsync(ct)).Items.Select(i => i.SchemaId.Value).ShouldBe(["unknown"]);

        var read = await admin.GetAsync(SchemaId.Create("Pre-Release"), ct);

        read.Found.ShouldBeTrue();
        read.Item!.SchemaId.Value.ShouldBe("pre-release");
        read.Item.AttributeDefinitions.Count.ShouldBe(1);
        read.Version!.Value.ShouldBe(2);
        (await ReadStoredDsoAsync(fixture, rowId, ct)).SchemaId.ShouldBe("pre-release");
        (await admin.QueryAsync(ct)).Items.Select(i => i.SchemaId.Value).ShouldBe(["pre-release"]);
    }

    [Fact]
    public async Task The_user_management_1_profile_schema_is_found_by_its_fixed_row_id_and_upconverted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        var store = fixture.ServiceProvider.GetRequiredService<ISchemaStore>();
        var profile = SchemaId.Create("user-profile");

        // User Management 1.x saved its profile schema under a fixed row id, with no key and no schema id.
        await SeedAsync(fixture, UserManagement1ProfileSchemaRowId, [], ct);

        (await admin.QueryAsync(ct)).Items.Select(i => i.SchemaId.Value).ShouldBe(["user-profile"]);

        var viaStore = await store.GetAsync(profile, ct);
        viaStore.AttributeDefinitions.Count.ShouldBe(1);

        // The first read stored the schema id and added the key, so later reads find it by key.
        (await ReadStoredDsoAsync(fixture, UserManagement1ProfileSchemaRowId, ct)).SchemaId.ShouldBe("user-profile");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, ct);
        var byKey = await storage.TryReadAsync(AttributeSchemaDso.EntityType, DataStorageKey.Create(SchemaIdDskV1.Create(profile)), ct);
        byKey.Found.ShouldBeTrue();
        byKey.Id.ShouldBe(UserManagement1ProfileSchemaRowId.Value);

        var read = await admin.GetAsync(profile, ct);
        var changed = ValidSchema("user-profile");
        changed.DisplayName = "Profile";
        (await admin.UpdateAsync(profile, changed, read.Version!.Value, ct)).IsSuccess.ShouldBeTrue();
        (await admin.GetAsync(profile, ct)).Item!.DisplayName.ShouldBe("Profile");
        (await admin.DeleteAsync(profile, ct)).IsSuccess.ShouldBeTrue();
        (await admin.QueryAsync(ct)).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_upconversion_write_still_returns_the_schema_and_logs_a_warning()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services =>
        {
            services.AddDynamicSchemaStorage();
            _ = services.AddFakeLogging();
            // The database refuses writes made after the row is seeded, as it would for a read-only user.
            _ = services.AddTransient<IPartitionedStorageFactory>(sp =>
                new FailingUpdatesStorageFactory(ActivatorUtilities.CreateInstance<DefaultPartitionedStorageFactory>(sp)));
        });
        var rowId = UuidV7.New();
        await SeedAsync(fixture, rowId, [DataStorageKey.Create(SchemaIdDskV1.Create(SchemaId.Create("pre-release")))], ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        var logs = fixture.ServiceProvider.GetFakeLogCollector();

        var read = await admin.GetAsync(SchemaId.Create("pre-release"), ct);

        read.Found.ShouldBeTrue();
        read.Item!.SchemaId.Value.ShouldBe("pre-release");
        read.Version!.Value.ShouldBe(1);
        (await ReadStoredDsoAsync(fixture, rowId, ct)).SchemaId.ShouldBeNull();
        var warning = logs.GetSnapshot().Where(r => r.Id.Name == "SchemaUpconversionFailed").ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        _ = warning.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task An_old_schema_can_be_updated_with_its_stored_version_without_reading_it_first()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        var profile = SchemaId.Create("user-profile");
        await SeedAsync(fixture, UserManagement1ProfileSchemaRowId, [], ct);
        var changed = ValidSchema("user-profile");
        changed.DisplayName = "Profile";

        var updated = await admin.UpdateAsync(profile, changed, (DataVersion)1, ct);

        updated.IsSuccess.ShouldBeTrue();
        (await ReadStoredDsoAsync(fixture, UserManagement1ProfileSchemaRowId, ct)).SchemaId.ShouldBe("user-profile");
        var read = await admin.GetAsync(profile, ct);
        read.Item!.DisplayName.ShouldBe("Profile");
        read.Version!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task A_newer_user_profile_schema_takes_precedence_over_the_user_management_1_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        await SeedAsync(fixture, UserManagement1ProfileSchemaRowId, [], ct);
        var newer = ValidSchema("user-profile");
        newer.DisplayName = "Newer";
        (await admin.CreateAsync(newer, ct)).IsSuccess.ShouldBeTrue();

        var read = await admin.GetAsync(SchemaId.Create("user-profile"), ct);

        read.Item!.DisplayName.ShouldBe("Newer");
        (await ReadStoredDsoAsync(fixture, UserManagement1ProfileSchemaRowId, ct)).SchemaId.ShouldBeNull();
        (await admin.QueryAsync(ct)).Items
            .Select(i => (i.SchemaId.Value, i.DisplayName))
            .ShouldBe([("user-profile", (string?)"Newer")]);
    }

    [Fact]
    public async Task Query_returns_every_schema_when_there_are_more_than_one_storage_page()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var admin = fixture.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        var ids = Enumerable.Range(0, DataRangeSize.MaxValue + 1).Select(i => $"schema-{i}").ToList();

        foreach (var id in ids)
        {
            (await admin.CreateAsync(ValidSchema(id), ct)).IsSuccess.ShouldBeTrue();
        }

        var query = await admin.QueryAsync(ct);

        query.Items.Select(i => i.SchemaId.Value).ShouldBe(ids, ignoreOrder: true);
        query.TotalCount.ShouldBe(ids.Count);
        query.HasMoreData.ShouldBeFalse();
    }
}
