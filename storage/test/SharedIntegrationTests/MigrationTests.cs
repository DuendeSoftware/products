// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.IntegrationTests;

public partial class MigrationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task migrate_creates_schema()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        await fixture.Schema.MigrateAsync(_ct);

        var result = await fixture.Schema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task migrate_is_idempotent()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        await fixture.Schema.MigrateAsync(_ct);
        await fixture.Schema.MigrateAsync(_ct);

        var result = await fixture.Schema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task build_migration_script_returns_executable_sql()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        var script = fixture.Schema.BuildMigrationScript(DatabaseSchemaVersion.Zero);
        script.ShouldNotBeNullOrWhiteSpace();

        await fixture.ExecuteSqlAsync(script, _ct);

        var result = await fixture.Schema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task migration_script_is_idempotent()
    {
        // All providers' scripts are self-contained and safe to execute twice:
        // MsSql/PostgreSql use version gates; SQLite uses IF NOT EXISTS.
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);

        var script = fixture.Schema.BuildMigrationScript(DatabaseSchemaVersion.Zero);

        await fixture.ExecuteSqlAsync(script, _ct);
        await fixture.ExecuteSqlAsync(script, _ct);

        var result = await fixture.Schema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }


}
