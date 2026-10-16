// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;

namespace Duende.Storage.Sqlite;

public sealed class LinkEntitiesHowToTests
{
    // begin-snippet: link-entities-define-dsos
    public static class OrganizationDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(5101, nameof(OrganizationDso));

        public sealed record V1(string Name) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }

    public static class ContributorDso
    {
        internal static readonly EntityType EntityType = new(5102, nameof(ContributorDso));

        public sealed record V1(string Name) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }

    public static class RepositoryDso
    {
        internal static readonly EntityType EntityType = new(5103, nameof(RepositoryDso));

        public sealed record V1(string Name) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    // begin-snippet: link-entities-define-links
    public static class OrganizationLinks
    {
        // Illustrative id only. Real link types must use an unused id from the owning product's reserved range.
        public static readonly LinkDefinition Membership = new()
        {
            Left = OrganizationDso.EntityType,
            Right = ContributorDso.EntityType,
            Link = new LinkType(5201, "Membership")
        };

        public static readonly LinkDefinition Assignment = new()
        {
            Left = ContributorDso.EntityType,
            Right = RepositoryDso.EntityType,
            Link = new LinkType(5202, "Assignment")
        };
    }
    // end-snippet

    [Fact]
    public async Task Can_link_and_unlink_entities()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
                services.AddDsoRegistration<RepositoryDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();

        _ = await storage.CreateAsync(acmeId, new OrganizationDso.V1("Acme"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(aliceId, new ContributorDso.V1("Alice"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);

        // begin-snippet: link-entities-link-and-unlink
        var linkResult = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);
        // linkResult is LinkResult.Success

        var duplicateResult = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);
        // duplicateResult is LinkResult.AlreadyLinked (the same LinkType, left, right triple already exists)

        var unlinkResult = await storage.UnlinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);
        // unlinkResult is UnlinkResult.Success, whether or not the link existed (idempotent)
        // end-snippet

        linkResult.ShouldBe(LinkResult.Success);
        duplicateResult.ShouldBe(LinkResult.AlreadyLinked);
        unlinkResult.ShouldBe(UnlinkResult.Success);

        // Unlinking something that was never linked also succeeds.
        var neverLinkedResult = await storage.UnlinkAsync(OrganizationLinks.Membership, acmeId, UuidV7.New(), [], ct);
        neverLinkedResult.ShouldBe(UnlinkResult.Success);
    }

    [Fact]
    public async Task Linking_does_not_require_either_entity_to_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        // Neither ID has a matching entity: Storage does not check referential integrity.
        var result = await storage.LinkAsync(OrganizationLinks.Membership, UuidV7.New(), UuidV7.New(), [], ct);

        result.ShouldBe(LinkResult.Success);
    }

    [Fact]
    public async Task Can_query_members_and_organizations_from_either_side()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();
        var bobId = UuidV7.New();

        _ = await storage.CreateAsync(acmeId, new OrganizationDso.V1("Acme"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(aliceId, new ContributorDso.V1("Alice"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(bobId, new ContributorDso.V1("Bob"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);

        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);
        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, bobId, [], ct);

        // begin-snippet: link-entities-query-reverse
        // "Which contributors belong to Acme?" (traverse Membership left-to-right)
        var membersQuery = LinkQuery.From(ContributorDso.EntityType)
            .Join(OrganizationLinks.Membership)
            .Where(OrganizationDso.EntityType, acmeId)
            .Build();

        var members = await storage.QueryLinksAsync<ContributorDso.V1>(
            membersQuery, DataRange.FromPage(1, 100), ct);

        // "Which organizations is Alice a member of?" (the same LinkDefinition, traversed right-to-left)
        var organizationsQuery = LinkQuery.From(OrganizationDso.EntityType)
            .Join(OrganizationLinks.Membership)
            .Where(ContributorDso.EntityType, aliceId)
            .Build();

        var organizations = await storage.QueryLinksAsync<OrganizationDso.V1>(
            organizationsQuery, DataRange.FromPage(1, 100), ct);
        // end-snippet

        members.Items.Count.ShouldBe(2);
        members.Items.Select(i => i.Value.Name).ShouldBe(["Alice", "Bob"], ignoreOrder: true);

        organizations.Items.Count.ShouldBe(1);
        organizations.Items[0].Value.Name.ShouldBe("Acme");
    }

    [Fact]
    public async Task Can_query_across_two_hops()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
                services.AddDsoRegistration<RepositoryDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();
        var bobId = UuidV7.New();
        var widgetApiId = UuidV7.New();

        _ = await storage.CreateAsync(acmeId, new OrganizationDso.V1("Acme"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(aliceId, new ContributorDso.V1("Alice"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(bobId, new ContributorDso.V1("Bob"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(widgetApiId, new RepositoryDso.V1("widget-api"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);

        // Alice is on Acme and assigned to widget-api; Bob is on Acme but not assigned to anything.
        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);
        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, bobId, [], ct);
        _ = await storage.LinkAsync(OrganizationLinks.Assignment, aliceId, widgetApiId, [], ct);

        // begin-snippet: link-entities-query-multi-hop
        // "Which repositories does Acme's membership touch?"
        // (Repository <- Assignment <- Contributor <- Membership <- Organization)
        var query = LinkQuery.From(RepositoryDso.EntityType)
            .Join(OrganizationLinks.Assignment)
            .Join(OrganizationLinks.Membership)
            .Where(OrganizationDso.EntityType, acmeId)
            .Build();

        var repositories = await storage.QueryLinksAsync<RepositoryDso.V1>(
            query, DataRange.FromPage(1, 100), ct);
        // end-snippet

        repositories.Items.Count.ShouldBe(1);
        repositories.Items[0].Value.Name.ShouldBe("widget-api");
    }

    [Fact]
    public async Task Deleting_an_entity_removes_its_links()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();

        _ = await storage.CreateAsync(acmeId, new OrganizationDso.V1("Acme"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(aliceId, new ContributorDso.V1("Alice"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);

        // Delete the LEFT side of the link (Acme), not Alice: the query below starts from the
        // contributor table and joins out to organizations, so it would still return Alice even
        // if her link row had survived a delete of the wrong entity. Deleting Acme instead proves
        // the link row itself is gone, not just that Alice's own row disappeared.
        _ = await storage.DeleteAsync(OrganizationDso.EntityType, acmeId, [], ct);

        var query = LinkQuery.From(ContributorDso.EntityType)
            .Join(OrganizationLinks.Membership)
            .Where(OrganizationDso.EntityType, acmeId)
            .Build();

        var members = await storage.QueryLinksAsync<ContributorDso.V1>(query, DataRange.FromPage(1, 100), ct);

        members.Items.ShouldBeEmpty();

        // Alice herself is untouched.
        (await storage.TryReadAsync(ContributorDso.EntityType, aliceId, ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Purging_a_pool_removes_links_too()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();

        _ = await storage.CreateAsync(acmeId, new OrganizationDso.V1("Acme"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(aliceId, new ContributorDso.V1("Alice"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);

        var purgeResult = await storage.PurgePoolAsync(ct);

        purgeResult.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(2);
        purgeResult.EntityLinksDeleted.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Can_link_atomically_with_other_operations_in_a_batch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();

        // begin-snippet: link-entities-link-in-batch
        var batchResult = await storage.ExecuteBatchAsync(
            [
                CreateOperation.For(acmeId, new OrganizationDso.V1("Acme"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                CreateOperation.For(aliceId, new ContributorDso.V1("Alice"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                LinkOperation.For(OrganizationLinks.Membership, acmeId, aliceId)
            ],
            [],
            ct);
        // end-snippet

        batchResult.Success.ShouldBeTrue();
        batchResult.Results.Count.ShouldBe(3);
        batchResult.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);
    }

    [Fact]
    public async Task Relinking_the_same_pair_in_a_batch_reports_already_linked_without_failing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmeId = UuidV7.New();
        var aliceId = UuidV7.New();

        _ = await storage.LinkAsync(OrganizationLinks.Membership, acmeId, aliceId, [], ct);

        // A LinkOperation for a pair that's already linked reports AlreadyLinked for that
        // operation, but does not fail the batch: the rest of the batch still commits.
        var batchResult = await storage.ExecuteBatchAsync(
            [LinkOperation.For(OrganizationLinks.Membership, acmeId, aliceId)],
            [],
            ct);

        batchResult.Success.ShouldBeTrue();
        batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.AlreadyLinked);
    }

    [Fact]
    public async Task QueryLinksAsync_only_supports_page_and_offset_ranges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<OrganizationDso.V1>();
                services.AddDsoRegistration<ContributorDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var query = LinkQuery.From(ContributorDso.EntityType)
            .Join(OrganizationLinks.Membership)
            .Where(OrganizationDso.EntityType, UuidV7.New())
            .Build();

        // Continuation-token ranges are not supported for link queries, only page and offset.
        _ = await Should.ThrowAsync<NotSupportedException>(() => storage.QueryLinksAsync<ContributorDso.V1>(
            query, DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 10), ct));

        // Page and offset ranges both work.
        var paged = await storage.QueryLinksAsync<ContributorDso.V1>(query, DataRange.FromPage(1, 10), ct);
        paged.Items.ShouldBeEmpty();
        var offset = await storage.QueryLinksAsync<ContributorDso.V1>(query, DataRange.FromOffset(0, 10), ct);
        offset.Items.ShouldBeEmpty();
    }
}
