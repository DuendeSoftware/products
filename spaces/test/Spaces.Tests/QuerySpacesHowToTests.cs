// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class QuerySpacesHowToTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = sc.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    private async Task CreateSpaceAsync(string name, string origin, bool enabled = true)
    {
        var created = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            _ct);
        created.IsSuccess.ShouldBeTrue();

        if (!enabled)
        {
            var getResult = await _admin.GetAsync(created.Id!, _ct);
            var space = getResult.Item!;
            space.Enabled = false;
            await _admin.UpdateAsync(created.Id!, space, getResult.Version!, _ct);
        }
    }

    [Fact]
    public async Task filter_matches_name_substring_and_enabled_status()
    {
        await CreateSpaceAsync("Acme Corp", "https://acme-corp.example.com");
        await CreateSpaceAsync("Acme Disabled", "https://acme-disabled.example.com", enabled: false);
        await CreateSpaceAsync("Globex", "https://globex.example.com");
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: query-spaces-filter
        // Name is a contains match. Enabled and IsDeleted are exact matches; leaving a
        // property null skips filtering on it.
        var result = await admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(
                new SpaceFilter { Name = "Acme", Enabled = true }),
            ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task default_page_size_is_25_and_paging_collects_every_space()
    {
        for (var i = 1; i <= 27; i++)
        {
            await CreateSpaceAsync($"Space {i:D2}", $"https://space{i}.example.com");
        }

        var firstPage = await _admin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(), _ct);
        firstPage.Items.Count.ShouldBe(25);
        firstPage.HasMoreData.ShouldBeTrue();
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: query-spaces-page-through-all
        var spaces = new List<SpaceListItem>();
        DataRange? range = null;

        do
        {
            var page = await admin.QueryAsync(
                new QueryRequest<SpaceFilter, SpaceSortField> { Range = range },
                ct);

            spaces.AddRange(page.Items);

            // HasMoreData tells you whether another page exists; NextToken carries the
            // continuation cursor to fetch it.
            range = page.HasMoreData && page.NextToken is { } nextToken
                ? DataRange.FromContinuationToken(nextToken)
                : null;
        }
        while (range is not null);
        // end-snippet

        spaces.Count.ShouldBe(27);
        spaces.Select(space => space.Id).Distinct().Count().ShouldBe(27);
    }

    [Fact]
    public async Task sorting_by_name_with_explicit_page_size_orders_and_pages_results()
    {
        foreach (var name in new[] { "Space C", "Space A", "Space B" })
        {
            await CreateSpaceAsync(name, $"https://{name.Replace(" ", "-")}.example.com");
        }

        var pageSize = (DataRangeSize)2;
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: query-spaces-sort-and-page-size
        // SortBy.Field requires an explicit field; without it, results come back in pool-ID
        // order and the page size defaults to 25.
        var firstPage = await admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(
                (SpaceFilter?)null,
                SortBy.Field(SpaceSortField.Name),
                DataRange.FromContinuationToken(ContinuationToken.Beginning, pageSize)),
            ct);
        // end-snippet

        firstPage.Items.Select(space => space.Name).ShouldBe(["Space A", "Space B"]);
        firstPage.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task paging_back_with_reversed_sort_returns_the_previous_page_in_original_order()
    {
        foreach (var name in new[] { "Space C", "Space A", "Space B" })
        {
            await CreateSpaceAsync(name, $"https://{name.Replace(" ", "-")}.example.com");
        }

        var ascendingSort = SortBy.Field(SpaceSortField.Name);
        var firstPage = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(
                (SpaceFilter?)null,
                ascendingSort,
                DataRange.FromContinuationToken(ContinuationToken.Beginning, (DataRangeSize)2)),
            _ct);

        var secondPage = await _admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(
                (SpaceFilter?)null,
                ascendingSort,
                DataRange.FromContinuationToken(firstPage.NextToken, (DataRangeSize)2)),
            _ct);

        secondPage.Items.Select(space => space.Name).ShouldBe(["Space C"]);
        secondPage.PreviousToken.ShouldNotBeNull();

        var previousToken = secondPage.PreviousToken!;
        var pageSize = (DataRangeSize)2;
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: query-spaces-page-back
        // Paging backwards means re-requesting with the reverse sort direction and the
        // page's PreviousToken. PreviousToken is only populated when an explicit sort was
        // requested: with no Sort, results use the default pool-ID order, which has no
        // reverse form, and PreviousToken is always null.
        var previousPage = await admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(
                (SpaceFilter?)null,
                SortBy.Field(SpaceSortField.Name, Duende.Storage.Querying.SortDirection.Descending),
                DataRange.FromContinuationToken(previousToken, pageSize)),
            ct);
        // end-snippet

        previousPage.Items.Select(space => space.Name).ShouldBe(["Space B", "Space A"]);
    }

    [Fact]
    public async Task previous_token_is_null_without_an_explicit_sort()
    {
        for (var i = 1; i <= 27; i++)
        {
            await CreateSpaceAsync($"Space {i:D2}", $"https://space{i}.example.com");
        }

        var page = await _admin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(), _ct);

        page.PreviousToken.ShouldBeNull();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static Task<QueryResult<SpaceListItem>> FindSpacesByDeletionStateAsync(
        ISpaceAdmin admin, bool? isDeleted, Ct ct)
    {
        // begin-snippet: query-spaces-deleted
        // IsDeleted: null includes every space regardless of deletion state, true returns
        // only logically deleted spaces, false excludes deleted spaces.
        return admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(
                new SpaceFilter { IsDeleted = isDeleted }),
            ct);
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task is_deleted_filter_distinguishes_deleted_active_and_all_spaces()
    {
        await _admin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Active", MatchPatterns = [new SpaceMatchPattern { Origin = "https://active.example.com" }] },
            _ct);
        var toDelete = await _admin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Deleted", MatchPatterns = [new SpaceMatchPattern { Origin = "https://deleted.example.com" }] },
            _ct);
        await _admin.DeleteAsync(toDelete.Id!, _ct);

        var onlyDeleted = await FindSpacesByDeletionStateAsync(_admin, true, _ct);
        onlyDeleted.Items.Select(s => s.Name).ShouldBe(["Deleted"]);

        var onlyActive = await FindSpacesByDeletionStateAsync(_admin, false, _ct);
        onlyActive.Items.Select(s => s.Name).ShouldBe(["Active"]);

        var all = await FindSpacesByDeletionStateAsync(_admin, null, _ct);
        all.Items.Count.ShouldBe(2);
    }
}
