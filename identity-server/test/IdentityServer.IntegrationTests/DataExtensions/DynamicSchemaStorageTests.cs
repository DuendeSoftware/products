// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions.Internal;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.IdentityServer.IntegrationTests.DataExtensions;

/// <summary>
/// Integration tests for the <c>Duende.IdentityServer.DataExtensions</c> copy of the dynamic (database-backed)
/// schema storage, verifying it against a real Sqlite-backed <see cref="IPartitionedStorageFactory"/>.
/// </summary>
public sealed class DynamicSchemaStorageTests(WebServerFixture webApp) : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static Duende.IdentityServer.DataExtensions.SchemaConfiguration ValidSchema(string id) => new()
    {
        SchemaId = Duende.IdentityServer.DataExtensions.SchemaId.Create(id),
        Groups =
        [
            new Duende.IdentityServer.DataExtensions.AttributeGroup(
                Duende.IdentityServer.DataExtensions.AttributeGroupCode.Create("general"),
                Duende.IdentityServer.DataExtensions.AttributeDisplayName.Create("General"),
                null,
                Order: 0)
        ],
        AttributeDefinitions =
        [
            new Duende.IdentityServer.DataExtensions.AttributeDefinition
            {
                Code = Duende.IdentityServer.DataExtensions.AttributeCode.Create("nickname"),
                AttributeType = new Duende.IdentityServer.DataExtensions.ScalarAttributeType(Duende.IdentityServer.DataExtensions.ScalarDataType.String),
            }
        ],
    };

    private async Task<KestrelBasedTestServer> CreateServerAsync(Action<IServiceCollection>? configureStorageFactory = null)
    {
        var output = TestContext.Current.TestOutputHelper!;
        var dbName = $"dynamic_schema_{Guid.NewGuid():N}";

        var server = new KestrelBasedTestServer(
            "dynamic-schema",
            webApp,
            new PrefixedTestOutputHelper(output, "dynamic-schema"),
            services =>
            {
                services.AddRouting();
                services.AddFakeLogging();

                services.AddIdentityServer()
                    .AddStorage(storage =>
                        storage.AddSqlite(opt =>
                            opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
                    .AddConfigurationStorage()
                    .AddOperationalStorage();

                services.AddDynamicSchemaStorage();

                configureStorageFactory?.Invoke(services);
            },
            _ => { });

        await server.StartAsync();

        var schema = server.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        return server;
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Query_returns_the_schema_id_and_display_name_of_every_saved_schema()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();

        var first = ValidSchema("tenant-a:Profile");
        first.DisplayName = "Tenant profile";
        var second = ValidSchema("device");
        var third = ValidSchema("Api-Scope");
        third.DisplayName = "API scope";
        third.Groups.Add(new Duende.IdentityServer.DataExtensions.AttributeGroup(
            Duende.IdentityServer.DataExtensions.AttributeGroupCode.Create("extra"), null, null, Order: 1));
        third.AttributeDefinitions.Add(new Duende.IdentityServer.DataExtensions.AttributeDefinition
        {
            Code = Duende.IdentityServer.DataExtensions.AttributeCode.Create("scope_name"),
            AttributeType = new Duende.IdentityServer.DataExtensions.ScalarAttributeType(Duende.IdentityServer.DataExtensions.ScalarDataType.String),
        });
        var schemas = new[] { first, second, third };

        foreach (var schema in schemas)
        {
            (await admin.CreateAsync(schema, _ct)).IsSuccess.ShouldBeTrue();
        }

        var result = await admin.QueryAsync(_ct);

        var expected = schemas
            .Select(s => (s.SchemaId.Value, s.DisplayName, s.AttributeDefinitions.Count, s.Groups.Count))
            .ToList();
        result.Results
            .Select(i => (i.SchemaId.Value, i.DisplayName, i.AttributeCount, i.GroupCount))
            .ToList()
            .ShouldBe(expected, ignoreOrder: true);
        result.TotalCount.ShouldBe(3);
        result.HasNextResults.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_returns_the_display_name_and_description_the_schema_was_created_with()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();

        var schema = ValidSchema("Customer-Profile");
        schema.DisplayName = "Customer profile";
        schema.Description = "Attributes collected at sign-up";
        (await admin.CreateAsync(schema, _ct)).IsSuccess.ShouldBeTrue();
        var requested = Duende.IdentityServer.DataExtensions.SchemaId.Create("customer-profile");

        var created = await admin.GetAsync(requested, _ct);

        created.Found.ShouldBeTrue();
        created.Item!.DisplayName.ShouldBe(schema.DisplayName);
        created.Item.Description.ShouldBe(schema.Description);
        created.Item.SchemaId.Value.ShouldBe(schema.SchemaId.Value);

        var changed = ValidSchema("Customer-Profile");
        changed.DisplayName = "Customer profile (renamed)";
        changed.Description = schema.Description;
        (await admin.UpdateAsync(changed.SchemaId, changed, created.Version!.Value, _ct)).IsSuccess.ShouldBeTrue();

        var updated = await admin.GetAsync(requested, _ct);

        updated.Found.ShouldBeTrue();
        updated.Item!.DisplayName.ShouldBe(changed.DisplayName);
        updated.Item.Description.ShouldBe(changed.Description);
        updated.Item.SchemaId.Value.ShouldBe(changed.SchemaId.Value);
    }

    private static readonly UuidV7 UserManagement1ProfileSchemaRowId =
        UuidV7.From(Guid.Parse("01968712-737f-70bf-a185-2da272266bd4"));

    private static Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeSchemaDso.V1 DsoWithoutSchemaId() =>
        new(null, null, null,
        [
            new Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeDefinitionDso.V1(
                "nickname", new Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeTypeDso("Scalar", "String", null, null, null, null),
                null, false, [], null, 0, null, false, false)
        ], []);

    private static async Task SeedAsync(IPartitionedStorageFactory factory, UuidV7 rowId, DataStorageKey[] keys, Ct ct)
    {
        var storage = await factory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, ct);
        var seeded = await storage.CreateAsync(rowId, DsoWithoutSchemaId(), keys, SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        seeded.ShouldBe(CreateResult.Success);
    }

    private static async Task<Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeSchemaDso.V1> ReadStoredDsoAsync(
        IPartitionedStorageFactory factory, UuidV7 rowId, Ct ct)
    {
        var storage = await factory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, ct);
        var stored = await storage.TryReadAsync(Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeSchemaDso.EntityType, rowId, ct);
        stored.Found.ShouldBeTrue();
        return stored.Dso.ShouldBeOfType<Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeSchemaDso.V1>();
    }

    [Fact]
    public async Task A_pre_release_schema_is_reported_as_unknown_until_it_is_read_by_id_and_upconverted()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
        var factory = server.GetRequiredService<IPartitionedStorageFactory>();
        var rowId = UuidV7.New();

        // Pre-release builds saved the schema with its key, but without the schema id in the document.
        await SeedAsync(factory, rowId, [DataStorageKey.Create(Duende.IdentityServer.DataExtensions.Internal.Storage.SchemaIdDskV1.Create(
            Duende.IdentityServer.DataExtensions.SchemaId.Create("pre-release")))], _ct);

        (await admin.QueryAsync(_ct)).Results.Select(i => i.SchemaId.Value).ShouldBe(["unknown"]);

        var read = await admin.GetAsync(Duende.IdentityServer.DataExtensions.SchemaId.Create("Pre-Release"), _ct);

        read.Found.ShouldBeTrue();
        read.Item!.SchemaId.Value.ShouldBe("pre-release");
        read.Item.AttributeDefinitions.Count.ShouldBe(1);
        read.Version!.Value.ShouldBe(2);
        (await ReadStoredDsoAsync(factory, rowId, _ct)).SchemaId.ShouldBe("pre-release");
        (await admin.QueryAsync(_ct)).Results.Select(i => i.SchemaId.Value).ShouldBe(["pre-release"]);
    }

    [Fact]
    public async Task The_user_management_1_profile_schema_is_found_by_its_fixed_row_id_and_upconverted()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
        var store = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaStore>();
        var factory = server.GetRequiredService<IPartitionedStorageFactory>();
        var profile = Duende.IdentityServer.DataExtensions.SchemaId.Create("user-profile");

        // User Management 1.x saved its profile schema under a fixed row id, with no key and no schema id.
        await SeedAsync(factory, UserManagement1ProfileSchemaRowId, [], _ct);

        (await admin.QueryAsync(_ct)).Results.Select(i => i.SchemaId.Value).ShouldBe(["user-profile"]);

        var viaStore = await store.GetAsync(profile, _ct);
        viaStore.AttributeDefinitions.Count.ShouldBe(1);

        // The first read stored the schema id and added the key, so later reads find it by key.
        (await ReadStoredDsoAsync(factory, UserManagement1ProfileSchemaRowId, _ct)).SchemaId.ShouldBe("user-profile");
        var storage = await factory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, _ct);
        var byKey = await storage.TryReadAsync(
            Duende.IdentityServer.DataExtensions.Internal.Storage.AttributeSchemaDso.EntityType,
            DataStorageKey.Create(Duende.IdentityServer.DataExtensions.Internal.Storage.SchemaIdDskV1.Create(profile)),
            _ct);
        byKey.Found.ShouldBeTrue();
        byKey.Id.ShouldBe(UserManagement1ProfileSchemaRowId.Value);

        var read = await admin.GetAsync(profile, _ct);
        var changed = ValidSchema("user-profile");
        changed.DisplayName = "Profile";
        (await admin.UpdateAsync(profile, changed, read.Version!.Value, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.GetAsync(profile, _ct)).Item!.DisplayName.ShouldBe("Profile");
        (await admin.DeleteAsync(profile, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.QueryAsync(_ct)).Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_upconversion_write_still_returns_the_schema_and_logs_a_warning()
    {
        // Real failure, not a fake: seed through a normal read-write connection to a file-backed
        // Sqlite database, close it, then reopen the same file read-only. The upconversion write
        // that GetAsync attempts genuinely fails (SQLITE_READONLY), while the read that precedes it
        // still succeeds, so this exercises the production try/catch without needing to implement
        // Duende.Storage's IPartitionedStorage (SetPoolId on that interface is internal to the
        // Storage assembly and IdentityServer.IntegrationTests has no InternalsVisibleTo access to it).
        var dbPath = Path.Combine(Path.GetTempPath(), $"dynamic_schema_ro_{Guid.NewGuid():N}.db");
        var rowId = UuidV7.New();
        try
        {
            await using (var seedProvider = BuildProvider($"Data Source={dbPath}"))
            {
                var schema = await seedProvider.GetRequiredService<IStorageInstanceSchemaFactory>()
                    .GetStorageInstanceSchema(StorageInstanceId.Default, _ct);
                await schema.MigrateAsync(_ct);

                var factory = seedProvider.GetRequiredService<IPartitionedStorageFactory>();
                await SeedAsync(factory, rowId, [DataStorageKey.Create(Duende.IdentityServer.DataExtensions.Internal.Storage.SchemaIdDskV1.Create(
                    Duende.IdentityServer.DataExtensions.SchemaId.Create("pre-release")))], _ct);
            }

            await using var readOnlyProvider = BuildProvider($"Data Source={dbPath};Mode=ReadOnly");
            var admin = readOnlyProvider.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
            var logs = readOnlyProvider.GetRequiredService<FakeLogCollector>();

            var read = await admin.GetAsync(Duende.IdentityServer.DataExtensions.SchemaId.Create("pre-release"), _ct);

            read.Found.ShouldBeTrue();
            read.Item!.SchemaId.Value.ShouldBe("pre-release");
            read.Version!.Value.ShouldBe(1);
            var readOnlyFactory = readOnlyProvider.GetRequiredService<IPartitionedStorageFactory>();
            (await ReadStoredDsoAsync(readOnlyFactory, rowId, _ct)).SchemaId.ShouldBeNull();
            var warning = logs.GetSnapshot().Where(r => r.Id.Name == "SchemaUpconversionFailed").ShouldHaveSingleItem();
            warning.Level.ShouldBe(LogLevel.Warning);
            _ = warning.Exception.ShouldNotBeNull();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFakeLogging();

        services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqlite(opt => opt.ConnectionString = connectionString));
        services.AddDynamicSchemaStorage();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task An_old_schema_can_be_updated_with_its_stored_version_without_reading_it_first()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
        var factory = server.GetRequiredService<IPartitionedStorageFactory>();
        var profile = Duende.IdentityServer.DataExtensions.SchemaId.Create("user-profile");
        await SeedAsync(factory, UserManagement1ProfileSchemaRowId, [], _ct);
        var changed = ValidSchema("user-profile");
        changed.DisplayName = "Profile";

        var updated = await admin.UpdateAsync(profile, changed, (Duende.IdentityServer.Models.DataVersion)1, _ct);

        updated.IsSuccess.ShouldBeTrue();
        (await ReadStoredDsoAsync(factory, UserManagement1ProfileSchemaRowId, _ct)).SchemaId.ShouldBe("user-profile");
        var read = await admin.GetAsync(profile, _ct);
        read.Item!.DisplayName.ShouldBe("Profile");
        read.Version!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task A_newer_user_profile_schema_takes_precedence_over_the_user_management_1_row()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
        var factory = server.GetRequiredService<IPartitionedStorageFactory>();
        await SeedAsync(factory, UserManagement1ProfileSchemaRowId, [], _ct);
        var newer = ValidSchema("user-profile");
        newer.DisplayName = "Newer";
        (await admin.CreateAsync(newer, _ct)).IsSuccess.ShouldBeTrue();

        var read = await admin.GetAsync(Duende.IdentityServer.DataExtensions.SchemaId.Create("user-profile"), _ct);

        read.Item!.DisplayName.ShouldBe("Newer");
        (await ReadStoredDsoAsync(factory, UserManagement1ProfileSchemaRowId, _ct)).SchemaId.ShouldBeNull();
        (await admin.QueryAsync(_ct)).Results
            .Select(i => (i.SchemaId.Value, i.DisplayName))
            .ShouldBe([("user-profile", (string?)"Newer")]);
    }

    [Fact]
    public async Task Attribute_definitions_and_groups_from_GetAsync_can_be_mutated_and_saved()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
        var schemaId = Duende.IdentityServer.DataExtensions.SchemaId.Create("mutable-schema");
        (await admin.CreateAsync(ValidSchema(schemaId.Value), _ct)).IsSuccess.ShouldBeTrue();

        var read = await admin.GetAsync(schemaId, _ct);
        read.Found.ShouldBeTrue();
        var schema = read.Item!;

        schema.AttributeDefinitions.Add(new Duende.IdentityServer.DataExtensions.AttributeDefinition
        {
            Code = Duende.IdentityServer.DataExtensions.AttributeCode.Create("second_attribute"),
            AttributeType = new Duende.IdentityServer.DataExtensions.ScalarAttributeType(Duende.IdentityServer.DataExtensions.ScalarDataType.String),
        });
        schema.Groups.Add(new Duende.IdentityServer.DataExtensions.AttributeGroup(
            Duende.IdentityServer.DataExtensions.AttributeGroupCode.Create("extra"), null, null, Order: 1));

        (await admin.UpdateAsync(schemaId, schema, read.Version!.Value, _ct)).IsSuccess.ShouldBeTrue();

        var updated = await admin.GetAsync(schemaId, _ct);
        updated.Found.ShouldBeTrue();
        updated.Item!.AttributeDefinitions.Select(a => a.Code.Value).ShouldBe(["nickname", "second_attribute"], ignoreOrder: true);
        updated.Item.Groups.Select(g => g.Code.Value).ShouldBe(["general", "extra"], ignoreOrder: true);

        var query = await admin.QueryAsync(_ct);
        query.Results.Single(i => i.SchemaId == schemaId).AttributeCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_returns_every_schema_when_there_are_more_than_one_storage_page()
    {
        await using var server = await CreateServerAsync();
        var admin = server.GetRequiredService<Duende.IdentityServer.DataExtensions.ISchemaAdmin>();
        var ids = Enumerable.Range(0, DataRangeSize.MaxValue + 1).Select(i => $"schema-{i}").ToList();

        foreach (var id in ids)
        {
            (await admin.CreateAsync(ValidSchema(id), _ct)).IsSuccess.ShouldBeTrue();
        }

        var query = await admin.QueryAsync(_ct);

        query.Results.Select(i => i.SchemaId.Value).ShouldBe(ids, ignoreOrder: true);
        query.TotalCount.ShouldBe(ids.Count);
        query.HasNextResults.ShouldBeFalse();
    }
}
