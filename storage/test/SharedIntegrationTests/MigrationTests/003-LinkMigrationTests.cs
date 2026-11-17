// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Schema;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Tests for the V003 migration that removes invalid links (rows whose left or right endpoint doesn't
/// exist as an entity of the declared type, in the declared pool) before adding the foreign keys and the
/// restrict_delete column. Seeds a V2 database by hand, through <see cref="IMigrationFixture.InsertLegacyLinkAsync"/>
/// (the real <c>LinkAsync</c> always writes the V3 restrict_delete column and so cannot run against a V2
/// schema), then migrates to the latest version and asserts exactly the valid rows survive.
/// </summary>
public partial class LinkMigrationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly LinkDefinition TestLink = TestLinkData.TestLink;

    private const string CountLinksSql = "SELECT COUNT(*) FROM entity_links";

    [Fact]
    public async Task Migration_removes_invalid_links_and_keeps_exactly_the_valid_onesAsync()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        await fixture.MigrateToAsync(2, _ct);

        var dataCategory = DataCategoryName.Create("link-migration");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        var linkTypeId = (int)TestLink.Link.Id;
        var leftTypeId = (int)TestLink.Left.Id;
        var rightTypeId = (int)TestLink.Right.Id;

        // 1. A valid link: both endpoints exist, correct types, same pool.
        var validLeft = UuidV7.New();
        var validRight = UuidV7.New();
        _ = await storage.CreateAsync(validLeft, new TestDso("valid-left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(validRight, new TestDso2("valid-right"), [], [], Expiration.NoExpiration, [], _ct);
        await fixture.InsertLegacyLinkAsync(PoolId.Default.Value, linkTypeId, leftTypeId, validLeft.Value, rightTypeId, validRight.Value, _ct);

        // 2. Orphan left: the right entity exists, the left id was never created.
        var orphanLeft = UuidV7.New();
        var rightOfOrphanLeft = UuidV7.New();
        _ = await storage.CreateAsync(rightOfOrphanLeft, new TestDso2("right-of-orphan-left"), [], [], Expiration.NoExpiration, [], _ct);
        await fixture.InsertLegacyLinkAsync(PoolId.Default.Value, linkTypeId, leftTypeId, orphanLeft.Value, rightTypeId, rightOfOrphanLeft.Value, _ct);

        // 3. Orphan right: the left entity exists, the right id was never created.
        var leftOfOrphanRight = UuidV7.New();
        var orphanRight = UuidV7.New();
        _ = await storage.CreateAsync(leftOfOrphanRight, new TestDso("left-of-orphan-right"), [], [], Expiration.NoExpiration, [], _ct);
        await fixture.InsertLegacyLinkAsync(PoolId.Default.Value, linkTypeId, leftTypeId, leftOfOrphanRight.Value, rightTypeId, orphanRight.Value, _ct);

        // 4. Type mismatch: the right id exists, but as the LEFT entity type (TestDso), not TestDso2.
        var leftOfMismatch = UuidV7.New();
        var mismatchedId = UuidV7.New();
        _ = await storage.CreateAsync(leftOfMismatch, new TestDso("left-of-mismatch"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(mismatchedId, new TestDso("exists-as-wrong-type"), [], [], Expiration.NoExpiration, [], _ct);
        await fixture.InsertLegacyLinkAsync(PoolId.Default.Value, linkTypeId, leftTypeId, leftOfMismatch.Value, rightTypeId, mismatchedId.Value, _ct);

        // 5. Valid, but in another pool: must survive the cleanup (it's valid in its own pool).
        var otherPoolLeft = UuidV7.New();
        var otherPoolRight = UuidV7.New();
        storage.SetPoolId(2);
        _ = await storage.CreateAsync(otherPoolLeft, new TestDso("other-pool-left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(otherPoolRight, new TestDso2("other-pool-right"), [], [], Expiration.NoExpiration, [], _ct);
        storage.SetPoolId(PoolId.Default);
        await fixture.InsertLegacyLinkAsync(2, linkTypeId, leftTypeId, otherPoolLeft.Value, rightTypeId, otherPoolRight.Value, _ct);

        // 6. Ids that only exist in another pool: invalid in the default pool, even though both ids exist somewhere.
        var crossPoolLeft = UuidV7.New();
        var crossPoolRight = UuidV7.New();
        storage.SetPoolId(3);
        _ = await storage.CreateAsync(crossPoolLeft, new TestDso("cross-pool-left"), [], [], Expiration.NoExpiration, [], _ct);
        storage.SetPoolId(PoolId.Default);
        _ = await storage.CreateAsync(crossPoolRight, new TestDso2("cross-pool-right"), [], [], Expiration.NoExpiration, [], _ct);
        await fixture.InsertLegacyLinkAsync(PoolId.Default.Value, linkTypeId, leftTypeId, crossPoolLeft.Value, rightTypeId, crossPoolRight.Value, _ct);

        // Before migration: all 6 rows are present, and a query by the orphan left id still returns the
        // (unjoined) right entity — the documented behaviour that V003 fixes.
        (await fixture.ExecuteScalarCountAsync(CountLinksSql, _ct)).ShouldBe(6);
        var beforeRows = await fixture.DumpEntityLinksAsync(_ct);
        beforeRows.Count.ShouldBe(6);
        var orphanLeftQuery = LinkQuery.From(TestDso2.DsoVersion.EntityType).Join(TestLink).Where(TestDso.DsoVersion.EntityType, orphanLeft).Build();
        (await storage.QueryLinksAsync(orphanLeftQuery, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);

        // Act: migrate to the latest version (runs V003).
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        // Assert: exactly the 2 valid rows (default pool + other pool) remain — compare the full row
        // set, not just a count, so the test also proves the cleanup kept exactly the right rows and
        // didn't, say, keep an orphan while dropping a valid one.
        (await fixture.ExecuteScalarCountAsync(CountLinksSql, _ct)).ShouldBe(2);

        var expectedRows = new[]
        {
            new EntityLinkRow(PoolId.Default.Value, linkTypeId, leftTypeId, validLeft.Value, rightTypeId, validRight.Value),
            new EntityLinkRow(2, linkTypeId, leftTypeId, otherPoolLeft.Value, rightTypeId, otherPoolRight.Value),
        };

        var afterRows = await fixture.DumpEntityLinksAsync(_ct);
        afterRows.ShouldBe(expectedRows, ignoreOrder: true);

        var validQuery = LinkQuery.From(TestDso2.DsoVersion.EntityType).Join(TestLink).Where(TestDso.DsoVersion.EntityType, validLeft).Build();
        (await storage.QueryLinksAsync(validQuery, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);

        storage.SetPoolId(2);
        var otherPoolQuery = LinkQuery.From(TestDso2.DsoVersion.EntityType).Join(TestLink).Where(TestDso.DsoVersion.EntityType, otherPoolLeft).Build();
        (await storage.QueryLinksAsync(otherPoolQuery, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);
        storage.SetPoolId(PoolId.Default);

        // The orphan-left query now returns nothing: the documented behaviour change.
        (await storage.QueryLinksAsync(orphanLeftQuery, DataRange.FromPage(1, 100), _ct)).Items.ShouldBeEmpty();

        var schemaResult = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        schemaResult.IsValid.ShouldBeTrue();
        schemaResult.Errors.ShouldBeEmpty();

        var versionResult = await fixture.StorageInstanceSchema.CheckVersionAsync(_ct);
        versionResult.CurrentVersion.ShouldBe(3u);
    }

    [Fact]
    public async Task Migration_cleanup_against_a_clean_V2_database_deletes_nothingAsync()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        await fixture.MigrateToAsync(2, _ct);

        var dataCategory = DataCategoryName.Create("link-migration-clean");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        await fixture.InsertLegacyLinkAsync(PoolId.Default.Value, (int)TestLink.Link.Id, (int)TestLink.Left.Id, leftId.Value, (int)TestLink.Right.Id, rightId.Value, _ct);

        (await fixture.ExecuteScalarCountAsync(CountLinksSql, _ct)).ShouldBe(1);

        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        (await fixture.ExecuteScalarCountAsync(CountLinksSql, _ct)).ShouldBe(1);
        var query = LinkQuery.From(TestDso2.DsoVersion.EntityType).Join(TestLink).Where(TestDso.DsoVersion.EntityType, leftId).Build();
        (await storage.QueryLinksAsync(query, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);
    }

    /// <summary>
    /// A rerun of the full migration script against an already-V3 database must not reset
    /// <c>restrict_delete</c> back to false or otherwise change the surviving rows, whether the
    /// rerun completes (PostgreSQL/MsSql/Oracle, gated on the stored version) or fails partway
    /// (SQLite, whose V002/V003 ADD COLUMN steps are unguarded — see the V003 script's own
    /// comment). Runs the full script twice, the same way
    /// <see cref="MigrationTests.migration_script_is_idempotent"/> does, but with real V3 data (a
    /// Restrict and a cascade link) written in between, instead of running it against bare V2 data.
    /// </summary>
    [Fact]
    public async Task Rerunning_the_migration_script_preserves_restrict_delete_on_existing_linksAsync()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var dataCategory = DataCategoryName.Create("link-migration-rerun");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        var restrictLink = TestLinkData.TestLinkRestrict;
        var restrictLeft = UuidV7.New();
        var restrictRight = UuidV7.New();
        _ = await storage.CreateAsync(restrictLeft, new TestDso("restrict-left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(restrictRight, new TestDso2("restrict-right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(restrictLink, restrictLeft, restrictRight, [], _ct)).ShouldBe(LinkResult.Success);

        var normalLeft = UuidV7.New();
        var normalRight = UuidV7.New();
        _ = await storage.CreateAsync(normalLeft, new TestDso("normal-left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(normalRight, new TestDso2("normal-right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(TestLink, normalLeft, normalRight, [], _ct)).ShouldBe(LinkResult.Success);

        var beforeRows = await fixture.DumpEntityLinksAsync(_ct);
        beforeRows.Count.ShouldBe(2);

        // Act: rerun the full migration script, the way migration_script_is_idempotent does.
        // PostgreSQL/MsSql/Oracle gate each step on the stored version and complete as a no-op;
        // SQLite's unguarded ADD COLUMN steps fail with "duplicate column name" instead (see
        // migration_script_is_idempotent and the V003 script's own comment) — either way, the
        // existing rows and their restrict_delete values must be untouched afterward. Only that one,
        // specific SQLite failure is tolerated: any other exception must fail the test.
        var script = fixture.StorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion.Zero);
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

        var afterRows = await fixture.DumpEntityLinksAsync(_ct);
        afterRows.ShouldBe(beforeRows, ignoreOrder: true);

        // The restricted link still refuses the delete of its right entity...
        (await storage.DeleteAsync(TestDso2.DsoVersion.EntityType, restrictRight, [], _ct)).ShouldBe(DeleteResult.StillLinked);
        (await storage.TryReadAsync(TestDso2.DsoVersion.EntityType, restrictRight, _ct)).Found.ShouldBeTrue();

        // ...while the cascade link still lets its right entity be deleted along with the link.
        (await storage.DeleteAsync(TestDso2.DsoVersion.EntityType, normalRight, [], _ct)).ShouldBe(DeleteResult.Success);
        (await storage.TryReadAsync(TestDso2.DsoVersion.EntityType, normalRight, _ct)).Found.ShouldBeFalse();

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
}
