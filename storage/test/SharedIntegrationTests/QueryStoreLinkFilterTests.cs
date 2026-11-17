// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Pagination;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Shared conformance tests for link filters: <c>QueryAsync</c>,
/// <c>QueryFieldsAsync</c> and <c>CountAsync</c> filtering by "has a link of a given
/// definition/direction to an entity matching a filter". Reuses <see cref="TestDso"/> as "resource"
/// (left side) and <see cref="TestDso2"/> as "scope" (right side), <see cref="TestLinkData.TestLink"/>
/// as the cascade definition and <see cref="TestLinkData.TestLinkRestrict"/> (link type id 9900) as the
/// restrict definition, so no new entity types or link definitions are needed.
/// </summary>
public partial class QueryStoreLinkFilterTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly EntityType ResourceType = TestDso.DsoVersion.EntityType;
    private static readonly EntityType ScopeType = TestDso2.DsoVersion.EntityType;
    private static readonly LinkDefinition Cascade = TestLinkData.TestLink;
    private static readonly LinkDefinition Restrict = TestLinkData.TestLinkRestrict;

    private async Task<IStorageFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    private static async Task<UuidV7> CreateResourceAsync(IPartitionedStorage storage, string name, Ct ct)
    {
        var id = UuidV7.New();
        await CreateResourceWithIdAsync(storage, id, name, ct);
        return id;
    }

    private static async Task CreateResourceWithIdAsync(IPartitionedStorage storage, UuidV7 id, string name, Ct ct)
    {
        var searchFields = new SearchFieldsBuilder().Add("name", name).Build();
        var result = await storage.CreateAsync(id, new TestDso(name), [], searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
    }

    private static async Task<UuidV7> CreateScopeAsync(IPartitionedStorage storage, string name, Ct ct)
    {
        var id = UuidV7.New();
        await CreateScopeWithIdAsync(storage, id, name, ct);
        return id;
    }

    private static async Task CreateScopeWithIdAsync(IPartitionedStorage storage, UuidV7 id, string name, Ct ct)
    {
        var searchFields = new SearchFieldsBuilder().Add("name", name).Build();
        var result = await storage.CreateAsync(id, new TestDso2(name), [], searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
    }

    // =========================================================================
    // Direction
    // =========================================================================

    [Fact]
    public async Task ResourcesLinkedToAScopeNamedAAreReturnedAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "r1", _ct);
        var r2 = await CreateResourceAsync(storage, "r2", _ct);
        var sa = await CreateScopeAsync(storage, "a", _ct);
        var sb = await CreateScopeAsync(storage, "b", _ct);

        _ = await storage.LinkAsync(Cascade, r1, sa, [], _ct);
        _ = await storage.LinkAsync(Cascade, r2, sb, [], _ct);

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").Equals("a"));
        var result = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Count.ShouldBe(1);
        ((TestDso)result.Items[0].Value).Value.ShouldBe("r1");
    }

    [Fact]
    public async Task ScopesLinkedFromAResourceMatchingAFilterAreReturnedAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "alice", _ct);
        var r2 = await CreateResourceAsync(storage, "bob", _ct);
        var s1 = await CreateScopeAsync(storage, "s1", _ct);
        var s2 = await CreateScopeAsync(storage, "s2", _ct);

        _ = await storage.LinkAsync(Cascade, r1, s1, [], _ct);
        _ = await storage.LinkAsync(Cascade, r2, s2, [], _ct);

        var filter = LinkFilter.ToLeft(Cascade, new StringField("name").Equals("alice"));
        var result = await storage.QueryAsync(ScopeType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Count.ShouldBe(1);
        ((TestDso2)result.Items[0].Value).Value.ShouldBe("s1");
    }

    // =========================================================================
    // Composition: And / Or / Not
    // =========================================================================

    [Fact]
    public async Task AndWithAResourceFieldNarrowsResultsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "r1", _ct);
        var r2 = await CreateResourceAsync(storage, "r2", _ct);
        var sa = await CreateScopeAsync(storage, "a", _ct);

        _ = await storage.LinkAsync(Cascade, r1, sa, [], _ct);
        _ = await storage.LinkAsync(Cascade, r2, sa, [], _ct);

        var filter = new StringField("name").Equals("r1")
            .And(LinkFilter.ToRight(Cascade, new StringField("name").Equals("a")));

        var result = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Count.ShouldBe(1);
        ((TestDso)result.Items[0].Value).Value.ShouldBe("r1");
    }

    [Fact]
    public async Task OrOfTwoLinkFiltersUnionsResultsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "r1", _ct);
        var r2 = await CreateResourceAsync(storage, "r2", _ct);
        var r3 = await CreateResourceAsync(storage, "r3", _ct);
        var sa = await CreateScopeAsync(storage, "a", _ct);
        var sb = await CreateScopeAsync(storage, "b", _ct);
        var sc = await CreateScopeAsync(storage, "c", _ct);

        _ = await storage.LinkAsync(Cascade, r1, sa, [], _ct);
        _ = await storage.LinkAsync(Cascade, r2, sb, [], _ct);
        _ = await storage.LinkAsync(Cascade, r3, sc, [], _ct);

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").Equals("a"))
            .Or(LinkFilter.ToRight(Cascade, new StringField("name").Equals("b")));

        var result = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Select(i => ((TestDso)i.Value).Value).ShouldBe(["r1", "r2"], ignoreOrder: true);
    }

    [Fact]
    public async Task NotOfALinkFilterExcludesLinkedResourcesAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "r1", _ct);
        var r2 = await CreateResourceAsync(storage, "r2", _ct);
        var sa = await CreateScopeAsync(storage, "a", _ct);

        _ = await storage.LinkAsync(Cascade, r1, sa, [], _ct);

        var filter = Query.Not(LinkFilter.ToRight(Cascade));
        var result = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Count.ShouldBe(1);
        ((TestDso)result.Items[0].Value).Value.ShouldBe("r2");
    }

    // =========================================================================
    // Sort + paging (offset and continuation token)
    // =========================================================================

    [Fact]
    public async Task SortByResourceFieldWithOffsetPagingAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var sa = await CreateScopeAsync(storage, "a", _ct);
        for (var i = 1; i <= 5; i++)
        {
            var r = await CreateResourceAsync(storage, $"r{i:D2}", _ct);
            _ = await storage.LinkAsync(Cascade, r, sa, [], _ct);
        }

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").Equals("a"));
        var sort = new SortParameter(new StringField("name"));

        var page1 = await storage.QueryAsync(ResourceType, filter, sort, DataRange.FromPage(1, 2), _ct);
        var page2 = await storage.QueryAsync(ResourceType, filter, sort, DataRange.FromPage(2, 2), _ct);
        var page3 = await storage.QueryAsync(ResourceType, filter, sort, DataRange.FromPage(3, 2), _ct);

        page1.TotalCount.ShouldBe(5);
        var names = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(i => ((TestDso)i.Value).Value).ToList();
        names.ShouldBe(["r01", "r02", "r03", "r04", "r05"]);
        names.Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task ContinuationTokenPagingAcrossThreePagesIsStableAndDuplicateFreeAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var sa = await CreateScopeAsync(storage, "a", _ct);
        for (var i = 1; i <= 7; i++)
        {
            var r = await CreateResourceAsync(storage, $"r{i:D2}", _ct);
            _ = await storage.LinkAsync(Cascade, r, sa, [], _ct);
        }

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").Equals("a"));
        var sort = new SortParameter(new StringField("name"));

        var seen = new List<string>();
        var token = ContinuationToken.Beginning;
        for (var i = 0; i < 4; i++)
        {
            var page = await storage.QueryAsync(ResourceType, filter, sort, DataRange.FromContinuationToken(token, 2), _ct);
            seen.AddRange(page.Items.Select(it => ((TestDso)it.Value).Value));
            if (page.NextToken is null)
            {
                break;
            }

            token = page.NextToken.Value;
        }

        seen.Count.ShouldBe(7);
        seen.Distinct().Count().ShouldBe(7);
        seen.ShouldBe(["r01", "r02", "r03", "r04", "r05", "r06", "r07"]);
    }

    // =========================================================================
    // TotalCount / CountAsync don't multiply rows
    // =========================================================================

    [Fact]
    public async Task TotalCountAndCountAsyncEqualDistinctMatchingResourcesAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "r1", _ct);
        var sa = await CreateScopeAsync(storage, "a1", _ct);
        var sb = await CreateScopeAsync(storage, "a2", _ct);
        var sc = await CreateScopeAsync(storage, "a3", _ct);

        // r1 links to three scopes that all match the filter — must still count as one resource.
        _ = await storage.LinkAsync(Cascade, r1, sa, [], _ct);
        _ = await storage.LinkAsync(Cascade, r1, sb, [], _ct);
        _ = await storage.LinkAsync(Cascade, r1, sc, [], _ct);

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").StartsWith("a"));

        var queried = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);
        var count = await storage.CountAsync(ResourceType, filter, _ct);

        queried.Items.Count.ShouldBe(1);
        queried.TotalCount.ShouldBe(1);
        count.ShouldBe(1);
    }

    // =========================================================================
    // QueryFieldsAsync
    // =========================================================================

    [Fact]
    public async Task QueryFieldsAsyncWithALinkFilterAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var r1 = await CreateResourceAsync(storage, "r1", _ct);
        var r2 = await CreateResourceAsync(storage, "r2", _ct);
        var sa = await CreateScopeAsync(storage, "a", _ct);

        _ = await storage.LinkAsync(Cascade, r1, sa, [], _ct);

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").Equals("a"));
        var fields = new List<Field> { new StringField("name") };

        var result = await storage.QueryFieldsAsync(ResourceType, fields, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Fields["NAME"].ShouldBe("R1");
    }

    // =========================================================================
    // Restrict definition only matches its own restrict_delete rows
    // =========================================================================

    [Fact]
    public async Task RestrictedDefinitionOnlyMatchesLinksWithRestrictDeleteAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var rCascade = await CreateResourceAsync(storage, "cascade-resource", _ct);
        var rRestrict = await CreateResourceAsync(storage, "restrict-resource", _ct);
        var scope = await CreateScopeAsync(storage, "shared-scope", _ct);

        // Same two entity types, different link type ids → different rows in entity_links; a
        // cascade link (restrict_delete = false) between rCascade and scope must not satisfy a
        // filter over the restrict definition.
        _ = await storage.LinkAsync(Cascade, rCascade, scope, [], _ct);
        _ = await storage.LinkAsync(Restrict, rRestrict, scope, [], _ct);

        var restrictFilter = LinkFilter.ToRight(Restrict, new StringField("name").Equals("shared-scope"));
        var result = await storage.QueryAsync(ResourceType, restrictFilter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);

        result.Items.Count.ShouldBe(1);
        ((TestDso)result.Items[0].Value).Value.ShouldBe("restrict-resource");
    }

    // =========================================================================
    // Pool isolation
    // =========================================================================

    [Fact]
    public async Task LinksInAnotherPoolDoNotMatchAsync()
    {
        // Same storage instance (same database), two different pools, reusing the same
        // entity ids in both pools: this is the only way to tell "the link filter correlates
        // on pool_id" apart from "QueryAsync happens to run against separate databases".
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var poolA = PoolId.Default;
        PoolId poolB = 2;

        var resourceId = UuidV7.New();
        var scopeId = UuidV7.New();

        storage.SetPoolId(poolA);
        await CreateResourceWithIdAsync(storage, resourceId, "r-a", _ct);
        await CreateScopeWithIdAsync(storage, scopeId, "a", _ct);
        (await storage.LinkAsync(Cascade, resourceId, scopeId, [], _ct)).ShouldBe(LinkResult.Success);

        // Pool B has the same ids, with the same resource/scope names, but no link between them.
        storage.SetPoolId(poolB);
        await CreateResourceWithIdAsync(storage, resourceId, "r-a", _ct);
        await CreateScopeWithIdAsync(storage, scopeId, "a", _ct);

        var filter = LinkFilter.ToRight(Cascade, new StringField("name").Equals("a"));

        var resultB = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);
        resultB.Items.ShouldBeEmpty();

        storage.SetPoolId(poolA);
        var resultA = await storage.QueryAsync(ResourceType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct);
        resultA.Items.Count.ShouldBe(1);
        ((TestDso)resultA.Items[0].Value).Value.ShouldBe("r-a");
    }

    // =========================================================================
    // Wrong-direction definition
    // =========================================================================

    [Fact]
    public async Task WrongDirectionDefinitionThrowsInvalidOperationExceptionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        // Cascade's Left is ResourceType, Right is ScopeType. Querying ScopeType with ToRight
        // (which expects the queried type to be Left) must throw.
        var filter = LinkFilter.ToRight(Cascade);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await storage.QueryAsync(ScopeType, filter, SortParameter.Empty, DataRange.FromOffset(0, 100), _ct));
    }
}
