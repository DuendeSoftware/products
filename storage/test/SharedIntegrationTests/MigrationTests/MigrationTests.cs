// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using System.Text.Json;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Schema;

namespace Duende.Storage.IntegrationTests;

public partial class MigrationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private const string LegacyMessageId = "018f06d3-5170-7bee-a0e3-9197ab10bcde";
    private const string LegacyEventId = "018f06d3-5170-7bee-a0e3-9197ab10bcdd";
    private const string LegacySubjectId = "018f06d3-5170-7bee-a0e3-9197ab10bcdf";

    private const string SchemaMissingMessage =
        "The database reports that a Duende Storage table does not exist. " +
        "The Storage schema may not have been created yet, or the connection string or SchemaName may point to another database or schema. " +
        "To create the schema, run 'duende storage migrate' or call IStorageInstanceSchema.MigrateAsync.";

    [Fact]
    public async Task migrate_creates_schema()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        var schemaVersionResult = await fixture.StorageInstanceSchema.CheckVersionAsync(_ct);
        schemaVersionResult.CurrentVersion.ShouldBe(0u,
            "Before test, current version should be 0");
        schemaVersionResult.IsCompatible.ShouldBeFalse();
        schemaVersionResult.RequiredVersion.ShouldBe(fixture.RequiredVersion);

        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        schemaVersionResult = await fixture.StorageInstanceSchema.CheckVersionAsync(_ct);
        schemaVersionResult.CurrentVersion.ShouldBe(fixture.RequiredVersion,
            "After migration, version should be updated");
    }

    [Fact]
    public async Task migrate_is_idempotent()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task build_migration_script_returns_executable_sql()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        var script = fixture.StorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion.Zero);
        script.ShouldNotBeNullOrWhiteSpace();

        await fixture.ExecuteMigrationScriptAsync(script, _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task migration_script_is_idempotent()
    {
        // PostgreSQL/MsSql/Oracle gate each step on the stored version, so rerunning the full
        // script completes as a no-op. SQLite's V002/V003 steps have an unguarded ALTER TABLE ADD
        // COLUMN (SQLite has no "ADD COLUMN IF NOT EXISTS"), so a second run fails with "duplicate
        // column name" at the first ADD COLUMN it reaches, before touching any data — an expected,
        // harmless failure (see the How-To doc "Migrations are idempotent in sequence, not safe to
        // run concurrently"), not a sign of corruption. Either way, the schema must still verify.
        //
        // Only that one, specific SQLite failure is tolerated here: any exception on another engine,
        // or any other exception on SQLite, means something broke (e.g. a provider's version gate)
        // and must fail the test instead of being swallowed.
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        var script = fixture.StorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion.Zero);

        await fixture.ExecuteMigrationScriptAsync(script, _ct);
        var rerunException = await Record.ExceptionAsync(() => fixture.ExecuteMigrationScriptAsync(script, _ct));

        if (fixture.MigrationScriptsAreRerunnable)
        {
            rerunException.ShouldBeNull();
        }
        else
        {
            _ = rerunException.ShouldNotBeNull();
            fixture.IsExpectedRerunFailure(rerunException!).ShouldBeTrue(
                $"Unexpected rerun failure: {rerunException}");
        }

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task migrate_from_v1_preserves_outbox_data_and_verifies_latest_schema()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await ApplyVersionOneAsync(fixture, _ct);

        var version = await fixture.StorageInstanceSchema.CheckVersionAsync(_ct);
        version.CurrentVersion.ShouldBe(1u);
        version.IsCompatible.ShouldBeFalse();
        await fixture.ExecuteSqlAsync(InsertLegacyOutboxSql(fixture), _ct);

        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        version = await fixture.StorageInstanceSchema.CheckVersionAsync(_ct);
        version.CurrentVersion.ShouldBe(fixture.RequiredVersion);
        version.IsCompatible.ShouldBeTrue();
        var verification = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        verification.IsValid.ShouldBeTrue();
        verification.Errors.ShouldBeEmpty();

        var newColumnValue = await fixture.ExecuteScalarAsync(
            $"SELECT dso_type_schema_version FROM {fixture.SchemaName}.outbox_subscriber_queue", _ct);
        newColumnValue.ShouldBe(DBNull.Value);

        var storage = await fixture.CrossPartitionStorageFactory.GetCrossPartitionStorageAsync(StorageInstanceId.Default, _ct);
        var page = await storage.GetOutboxEventsForSubscriptionAsync(SubscriberName.Create("legacy-subscriber"), 10, _ct);
        page.HasMore.ShouldBeFalse();
        var row = page.Events.ShouldHaveSingleItem();
        using var payload = JsonDocument.Parse(row.Payload);
        payload.RootElement.GetProperty("value").GetString().ShouldBe("preserved");
    }

    [Fact]
    public async Task verify_reports_nullability_changes_in_both_directions()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(ChangeNullabilitySql(fixture), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        var requiredColumn = result.Errors.Single(e =>
            string.Equals(e.Table, "outbox_subscriber_queue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Column, "event_name", StringComparison.OrdinalIgnoreCase));
        requiredColumn.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        requiredColumn.ErrorMessage.ShouldContain("is nullable, expected not nullable");

        var nullableColumn = result.Errors.Single(e =>
            string.Equals(e.Table, "outbox_subscriber_queue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Column, "dso_type_schema_version", StringComparison.OrdinalIgnoreCase));
        nullableColumn.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        nullableColumn.ErrorMessage.ShouldContain("is not nullable, expected nullable");
    }

    [Fact]
    public async Task verify_reports_missing_and_wrong_primary_key()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(DropPrimaryKeySql(fixture), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var missing = result.Errors.ShouldHaveSingleItem();
        missing.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        missing.Table.ShouldBe("entity_links");
        missing.ErrorMessage.ShouldContain("Primary key on table 'entity_links' is missing");
        missing.ErrorMessage.ShouldContain("expected (pool_id, link_type_id, left_entity_id, right_entity_id)", Case.Insensitive);

        await fixture.ExecuteSqlAsync(AddWrongPrimaryKeySql(fixture), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var wrong = result.Errors.ShouldHaveSingleItem();
        wrong.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        wrong.Table.ShouldBe("entity_links");
        wrong.ErrorMessage.Contains(
            "has columns (link_type_id, pool_id, left_entity_id, right_entity_id)",
            StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.ShouldContain("expected (pool_id, link_type_id, left_entity_id, right_entity_id)", Case.Insensitive);

        await fixture.ExecuteSqlAsync(DropPrimaryKeySql(fixture), _ct);
        await fixture.ExecuteSqlAsync(AddPrimaryKeyWithWrongColumnSql(fixture), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        wrong = result.Errors.ShouldHaveSingleItem();
        wrong.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        wrong.Table.ShouldBe("entity_links");
        wrong.ErrorMessage.Contains(
            "has columns (pool_id, link_type_id, left_entity_id, right_entity_type_id)",
            StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.ShouldContain("expected (pool_id, link_type_id, left_entity_id, right_entity_id)", Case.Insensitive);
    }

    [Fact]
    public async Task verify_reports_missing_and_wrong_outbox_message_id_unique_constraint()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(DropUniqueConstraintSql(fixture), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var missing = result.Errors.ShouldHaveSingleItem();
        missing.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        missing.Table.ShouldBe("outbox_subscriber_queue");
        missing.ErrorMessage.ShouldContain("Unique constraint on table 'outbox_subscriber_queue' is missing");
        missing.ErrorMessage.ShouldContain("expected (message_id)", Case.Insensitive);

        await fixture.ExecuteSqlAsync(AddWrongUniqueConstraintSql(fixture), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var wrong = result.Errors.ShouldHaveSingleItem();
        wrong.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        wrong.Table.ShouldBe("outbox_subscriber_queue");
        wrong.ErrorMessage.Contains("has columns (event_id)", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.ShouldContain("expected (message_id)", Case.Insensitive);
    }

    [Fact]
    public async Task verify_reports_foreign_key_with_wrong_delete_rule()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(ChangeForeignKeyDeleteRuleSql(fixture), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        error.Table.ShouldBe("entity_keys");
        error.ErrorMessage.Replace('_', ' ').ShouldContain("delete rule 'NO ACTION', expected 'CASCADE'");
    }

    [Fact]
    public async Task verify_reports_missing_index_and_same_name_with_wrong_key_columns()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(DropCreatedAtIndexSql(fixture), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var missing = result.Errors.ShouldHaveSingleItem();
        missing.Kind.ShouldBe(SchemaVerificationErrorKind.MissingIndex);
        missing.ErrorMessage.Contains(CreatedAtIndexName(fixture), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        missing.ErrorMessage.ShouldContain("is missing");

        await fixture.ExecuteSqlAsync(CreateWrongCreatedAtIndexSql(fixture), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var wrong = result.Errors.ShouldHaveSingleItem();
        wrong.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        string.Equals(wrong.Table, "entities", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.Contains(CreatedAtIndexName(fixture), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.Contains(
            "has key columns (pool_id, created_at, entity_type_id)",
            StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.ShouldContain("expected (pool_id, entity_type_id, created_at)", Case.Insensitive);

        await fixture.ExecuteSqlAsync(DropCreatedAtIndexSql(fixture), _ct);
        await fixture.ExecuteSqlAsync(CreateIndexWithWrongColumnSql(fixture), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        wrong = result.Errors.ShouldHaveSingleItem();
        wrong.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        string.Equals(wrong.Table, "entities", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.Contains(CreatedAtIndexName(fixture), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.Contains(
            "has key columns (pool_id, entity_type_id, expires_at)",
            StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.ShouldContain("expected (pool_id, entity_type_id, created_at)", Case.Insensitive);

        await fixture.ExecuteSqlAsync(DropCreatedAtIndexSql(fixture), _ct);
        await fixture.ExecuteSqlAsync(CreateIndexWithExtraKeySql(fixture), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        wrong = result.Errors.ShouldHaveSingleItem();
        wrong.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        string.Equals(wrong.Table, "entities", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.Contains(CreatedAtIndexName(fixture), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.Contains(
            "has key columns (pool_id, entity_type_id, created_at, value_version)",
            StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        wrong.ErrorMessage.ShouldContain("expected (pool_id, entity_type_id, created_at)", Case.Insensitive);
    }

    [Fact]
    public async Task read_by_key_before_migrate_explains_how_to_migrate()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        var dataCategory = DataCategoryName.Create("some_category");
        var key = DataStorageKey.Create(new TestJsonKeyDsk("some_key"));
        var partitionedStorage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, key, _ct));

        exception.Message.ShouldBe(SchemaMissingMessage);
        _ = exception.InnerException.ShouldBeAssignableTo<DbException>();
    }

    [Fact]
    public async Task read_many_before_migrate_explains_how_to_migrate()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        var dataCategory = DataCategoryName.Create("some_category");
        HashSet<UuidV7> ids = [UuidV7.New()];
        var partitionedStorage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => partitionedStorage.TryReadManyAsync(TestDso.DsoVersion.EntityType, ids, maximum: 10, _ct));

        exception.Message.ShouldBe(SchemaMissingMessage);
        _ = exception.InnerException.ShouldBeAssignableTo<DbException>();
    }

    [Fact]
    public async Task read_outbox_before_migrate_explains_how_to_migrate()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        var subscriberName = SubscriberName.Create("some_subscriber");
        var crossPartitionStorage = await fixture.CrossPartitionStorageFactory.GetCrossPartitionStorageAsync(StorageInstanceId.Default, _ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriberName, count: 10, _ct));

        exception.Message.ShouldBe(SchemaMissingMessage);
        _ = exception.InnerException.ShouldBeAssignableTo<DbException>();
    }

    [Fact]
    public async Task read_after_a_column_was_dropped_passes_the_database_error_through()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.DropColumnAsync("entities", "value_version", _ct);
        var dataCategory = DataCategoryName.Create("some_category");
        var partitionedStorage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        _ = await Should.ThrowAsync<DbException>(
            () => partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, UuidV7.New(), _ct));
    }
}
