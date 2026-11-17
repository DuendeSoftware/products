// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Internal.Querying;

namespace Duende.Storage.IntegrationTests;

public partial class StoreLinkQueryTests
{
    private async Task<UuidV7> CreateUserAsync(IPartitionedStorage storage, string name)
    {
        var id = UuidV7.New();
        _ = await storage.CreateAsync(id, new UserDso(name), [], [], Expiration.NoExpiration, [], _ct);
        return id;
    }

    private async Task<UuidV7> CreateGroupAsync(IPartitionedStorage storage, string name)
    {
        var id = UuidV7.New();
        _ = await storage.CreateAsync(id, new GroupDso(name), [], [], Expiration.NoExpiration, [], _ct);
        return id;
    }

    private async Task<UuidV7> CreateRoleAsync(IPartitionedStorage storage, string name)
    {
        var id = UuidV7.New();
        _ = await storage.CreateAsync(id, new RoleDso(name), [], [], Expiration.NoExpiration, [], _ct);
        return id;
    }

    private static LinkQueryDescriptor UsersByGroups(params UuidV7[] groupIds) =>
        LinkQuery.From(User)
            .Join(UserGroup)
            .WhereAny(Group, groupIds.ToHashSet())
            .Build();

    [Fact]
    public async Task Entities_are_paired_with_the_anchor_they_were_reached_from()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var engineers = await CreateGroupAsync(storage, "engineers");
        var designers = await CreateGroupAsync(storage, "designers");
        var empty = await CreateGroupAsync(storage, "empty");
        var alice = await CreateUserAsync(storage, "alice");
        var bob = await CreateUserAsync(storage, "bob");
        var carol = await CreateUserAsync(storage, "carol");

        _ = await storage.LinkAsync(UserGroup, alice, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, designers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, carol, designers, [], _ct);

        var result = await storage.QueryLinksByAnchorAsync(
            UsersByGroups(engineers, designers, empty), DataRange.FromPage(1, 100), _ct);

        var pairs = result.Items.Select(i => (i.AnchorId, ((UserDso)i.Entity.Value).Name)).ToList();
        pairs.Count.ShouldBe(4);
        pairs.ShouldContain((engineers, "alice"));
        pairs.ShouldContain((engineers, "bob"));
        pairs.ShouldContain((designers, "bob"));
        pairs.ShouldContain((designers, "carol"));
        pairs.ShouldNotContain(p => p.AnchorId == empty);
        result.TotalCount.ShouldBeNull();
        result.TotalPages.ShouldBeNull();
    }

    [Fact]
    public async Task Entities_are_not_returned_for_anchors_that_were_not_requested()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var requested = await CreateGroupAsync(storage, "requested");
        var other = await CreateGroupAsync(storage, "other");
        var alice = await CreateUserAsync(storage, "alice");
        var bob = await CreateUserAsync(storage, "bob");
        _ = await storage.LinkAsync(UserGroup, alice, requested, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, other, [], _ct);

        var result = await storage.QueryLinksByAnchorAsync(
            UsersByGroups(requested), DataRange.FromPage(1, 100), _ct);

        var item = result.Items.ShouldHaveSingleItem();
        item.AnchorId.ShouldBe(requested);
        ((UserDso)item.Entity.Value).Name.ShouldBe("alice");
    }

    [Fact]
    public async Task Multi_hop_pairs_entities_with_the_terminal_anchor()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var admin = await CreateRoleAsync(storage, "admin");
        var viewer = await CreateRoleAsync(storage, "viewer");
        var engineers = await CreateGroupAsync(storage, "engineers");
        var designers = await CreateGroupAsync(storage, "designers");
        var alice = await CreateUserAsync(storage, "alice");
        var bob = await CreateUserAsync(storage, "bob");

        _ = await storage.LinkAsync(UserGroup, alice, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, designers, [], _ct);
        _ = await storage.LinkAsync(GroupRole, engineers, admin, [], _ct);
        _ = await storage.LinkAsync(GroupRole, designers, viewer, [], _ct);

        var query = LinkQuery.From(User)
            .Join(UserGroup)
            .Join(GroupRole)
            .WhereAny(Role, new HashSet<UuidV7> { admin, viewer })
            .Build();

        var result = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(1, 100), _ct);

        var pairs = result.Items.Select(i => (i.AnchorId, ((UserDso)i.Entity.Value).Name)).ToList();
        pairs.Count.ShouldBe(2);
        pairs.ShouldContain((admin, "alice"));
        pairs.ShouldContain((viewer, "bob"));
    }

    [Fact]
    public async Task Paging_walks_all_pairs_in_order_and_reports_neighbouring_pages()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var engineers = await CreateGroupAsync(storage, "engineers");
        var designers = await CreateGroupAsync(storage, "designers");
        for (var i = 0; i < 3; i++)
        {
            var user = await CreateUserAsync(storage, $"user-{i}");
            _ = await storage.LinkAsync(UserGroup, user, engineers, [], _ct);
            _ = await storage.LinkAsync(UserGroup, user, designers, [], _ct);
        }

        var query = UsersByGroups(engineers, designers);

        var all = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(1, 100), _ct);
        var page1 = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(1, 3), _ct);
        var page2 = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(2, 3), _ct);

        all.Items.Count.ShouldBe(6);
        all.HasMoreData.ShouldBeFalse();
        all.HasPreviousData.ShouldBeFalse();

        page1.Items.Count.ShouldBe(3);
        page1.HasMoreData.ShouldBeTrue();
        page1.HasPreviousData.ShouldBeFalse();

        page2.Items.Count.ShouldBe(3);
        page2.HasMoreData.ShouldBeFalse();
        page2.HasPreviousData.ShouldBeTrue();

        var beyondEnd = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(3, 3), _ct);
        beyondEnd.Items.ShouldBeEmpty();
        beyondEnd.HasPreviousData.ShouldBeFalse();

        static (UuidV7, UuidV7) Key(LinkedEnvelope i) => (i.AnchorId, i.Entity.Id);
        page1.Items.Concat(page2.Items).Select(Key).ShouldBe(all.Items.Select(Key));

        var anchors = all.Items.Select(i => i.AnchorId).ToList();
        anchors.Distinct().Count().ShouldBe(2);
        anchors.Where((a, i) => i > 0 && a != anchors[i - 1]).Count().ShouldBe(1);
    }

    [Fact]
    public async Task Paging_dedupes_entities_reached_through_two_hops_across_page_boundaries()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var admin = await CreateRoleAsync(storage, "admin");
        var engineers = await CreateGroupAsync(storage, "engineers");
        var designers = await CreateGroupAsync(storage, "designers");
        var qa = await CreateGroupAsync(storage, "qa");
        var alice = await CreateUserAsync(storage, "alice");
        var bob = await CreateUserAsync(storage, "bob");
        var carol = await CreateUserAsync(storage, "carol");

        // Alice reaches "admin" through both "engineers" and "designers", creating a duplicate
        // (admin, alice) pair before DISTINCT removes it.
        _ = await storage.LinkAsync(UserGroup, alice, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, alice, designers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, designers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, carol, qa, [], _ct);
        _ = await storage.LinkAsync(GroupRole, engineers, admin, [], _ct);
        _ = await storage.LinkAsync(GroupRole, designers, admin, [], _ct);
        _ = await storage.LinkAsync(GroupRole, qa, admin, [], _ct);

        var query = LinkQuery.From(User)
            .Join(UserGroup)
            .Join(GroupRole)
            .WhereAny(Role, new HashSet<UuidV7> { admin })
            .Build();

        var all = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(1, 100), _ct);

        // Small page size relative to the (pre-distinct) duplicate rows, so the duplicate
        // (admin, alice) pair straddles a page boundary.
        var pages = new List<LinkedEnvelope>();
        var pageNumber = 1;
        while (true)
        {
            var page = await storage.QueryLinksByAnchorAsync(query, DataRange.FromPage(pageNumber, 1), _ct);
            pages.AddRange(page.Items);
            if (!page.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        static (UuidV7 AnchorId, UuidV7 EntityId) Key(LinkedEnvelope i) => (i.AnchorId, i.Entity.Id);

        pages.Select(Key).ShouldBe(all.Items.Select(Key));
        pages.Select(Key).Distinct().Count().ShouldBe(pages.Count);

        var names = pages.Select(i => ((UserDso)i.Entity.Value).Name).ToList();
        names.Count.ShouldBe(3);
        names.ShouldContain("alice");
        names.ShouldContain("bob");
        names.ShouldContain("carol");
    }

    [Fact]
    public async Task Empty_anchor_set_returns_an_empty_result()
    {
        await using var fixture = await CreateProviderAsync();

        var result = await fixture.PartitionedStorage.QueryLinksByAnchorAsync(
            UsersByGroups(), DataRange.FromPage(1, 100), _ct);

        result.Items.ShouldBeEmpty();
        result.HasMoreData.ShouldBeFalse();
        result.HasPreviousData.ShouldBeFalse();
    }

    [Fact]
    public async Task Query_by_anchor_rejects_a_descriptor_without_anchors()
    {
        await using var fixture = await CreateProviderAsync();
        var query = LinkQuery.From(User).Join(UserGroup).Where(Group, UuidV7.New()).Build();

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.PartitionedStorage.QueryLinksByAnchorAsync(query, DataRange.FromPage(1, 100), _ct));
    }

    [Fact]
    public async Task Query_by_anchor_rejects_continuation_tokens()
    {
        await using var fixture = await CreateProviderAsync();

        _ = await Should.ThrowAsync<NotSupportedException>(() =>
            fixture.PartitionedStorage.QueryLinksByAnchorAsync(
                UsersByGroups(UuidV7.New()), DataRange.FromContinuationToken(ContinuationToken.Beginning, 10), _ct));
    }

    [Fact]
    public async Task Query_links_rejects_a_descriptor_with_anchors()
    {
        await using var fixture = await CreateProviderAsync();

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.PartitionedStorage.QueryLinksAsync(
                UsersByGroups(UuidV7.New()), DataRange.FromPage(1, 100), _ct));
    }

    [Fact]
    public async Task Count_returns_distinct_entities_per_anchor_including_zero()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var engineers = await CreateGroupAsync(storage, "engineers");
        var designers = await CreateGroupAsync(storage, "designers");
        var empty = await CreateGroupAsync(storage, "empty");
        var alice = await CreateUserAsync(storage, "alice");
        var bob = await CreateUserAsync(storage, "bob");

        _ = await storage.LinkAsync(UserGroup, alice, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, bob, designers, [], _ct);

        var counts = await storage.CountLinksAsync(UsersByGroups(engineers, designers, empty), _ct);

        counts.Count.ShouldBe(3);
        counts[engineers].ShouldBe(2);
        counts[designers].ShouldBe(1);
        counts[empty].ShouldBe(0);
    }

    [Fact]
    public async Task Count_over_multiple_hops_counts_each_source_entity_once_per_anchor()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var admin = await CreateRoleAsync(storage, "admin");
        var engineers = await CreateGroupAsync(storage, "engineers");
        var designers = await CreateGroupAsync(storage, "designers");
        var alice = await CreateUserAsync(storage, "alice");

        _ = await storage.LinkAsync(UserGroup, alice, engineers, [], _ct);
        _ = await storage.LinkAsync(UserGroup, alice, designers, [], _ct);
        _ = await storage.LinkAsync(GroupRole, engineers, admin, [], _ct);
        _ = await storage.LinkAsync(GroupRole, designers, admin, [], _ct);

        var query = LinkQuery.From(User)
            .Join(UserGroup)
            .Join(GroupRole)
            .WhereAny(Role, new HashSet<UuidV7> { admin })
            .Build();

        var counts = await storage.CountLinksAsync(query, _ct);

        counts[admin].ShouldBe(1);
    }

    [Fact]
    public async Task Count_of_an_empty_anchor_set_is_empty()
    {
        await using var fixture = await CreateProviderAsync();

        var counts = await fixture.PartitionedStorage.CountLinksAsync(UsersByGroups(), _ct);

        counts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Count_rejects_a_descriptor_without_anchors()
    {
        await using var fixture = await CreateProviderAsync();
        var query = LinkQuery.From(User).Join(UserGroup).Where(Group, UuidV7.New()).Build();

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.PartitionedStorage.CountLinksAsync(query, _ct));
    }
}
