// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;

namespace Duende.Storage.IntegrationTests;

public partial class MigrationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

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

        await fixture.ExecuteSqlAsync(script, _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task migration_script_is_idempotent()
    {
        // All providers' scripts are self-contained and safe to execute twice:
        // MsSql/PostgreSql use version gates; SQLite uses IF NOT EXISTS.
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        var script = fixture.StorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion.Zero);

        await fixture.ExecuteSqlAsync(script, _ct);
        await fixture.ExecuteSqlAsync(script, _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }


}
