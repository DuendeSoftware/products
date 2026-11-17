// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using SortDirection = Duende.Storage.Internal.Querying.SortDirection;

namespace Duende.Storage.Sqlite;

public sealed class SearchAndQueryDsosHowToTests
{
    // begin-snippet: search-article-dso
    public static class ArticleDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(9001, nameof(ArticleDso));

        public sealed record V1(
            string Title,
            string Author,
            DateTimeOffset PublishedAt,
            IReadOnlyList<string> Tags,
            IReadOnlyDictionary<string, string> CustomFields) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    // begin-snippet: query-map-stored-version
    private static ArticleDso.V1 ToArticle(IDataStorageObject dso) => dso switch
    {
        ArticleDso.V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected DSO type {dso.GetType().Name}.")
    };
    // end-snippet
    // begin-snippet: search-fields-definitions
    private static class Fields
    {
        public static readonly StringField Title = new("title");
        public static readonly ExactMatchField Author = new("author");
        public static readonly DateTimeField PublishedAt = new("publishedAt");
        public static readonly StringArrayField Tags = new("tags");
    }
    // end-snippet

    private static SearchFieldCollection BuildSearchFields(ArticleDso.V1 article)
    {
        var builder = new SearchFieldsBuilder()
            .Add(Fields.Title.Path, article.Title)
            .Add(Fields.PublishedAt.Path, article.PublishedAt);
        _ = builder.AddExactMatch(Fields.Author.Path, article.Author);

        var tagIndex = 0;
        foreach (var tag in article.Tags)
        {
            _ = builder.Add(Fields.Tags.Path, tagIndex++, tag);
        }

        return builder.Build();
    }

    [Fact]
    public async Task Building_search_fields_covers_every_field_kind()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var article = new ArticleDso.V1(
            "Configuring Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ["storage", "how-to"],
            new Dictionary<string, string>());

        // begin-snippet: write-search-fields
        var builder = new SearchFieldsBuilder()
            // StringField: substring/prefix/suffix search, but the stored value is
            // upper-invariant, not the raw string (see SearchFieldValue).
            .Add(Fields.Title.Path, article.Title)
            // DateTimeField: range and equality comparisons.
            .Add(Fields.PublishedAt.Path, article.PublishedAt);

        // ExactMatchField: only equality/"in" lookups are needed, so store a deterministic
        // hash instead of the full string. Cheaper to index and to compare than StringField.
        _ = builder.AddExactMatch(Fields.Author.Path, article.Author);

        // StringArrayField: one row per array item, all sharing the same field path.
        var tagIndex = 0;
        foreach (var tag in article.Tags)
        {
            _ = builder.Add(Fields.Tags.Path, tagIndex++, tag);
        }

        var searchFields = builder.Build();
        // end-snippet

        var id = UuidV7.New();
        _ = await storage.CreateAsync(id, article, [], searchFields, Expiration.NoExpiration, [], ct);

        var byTitle = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Title.Contains("Configuring"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byTitle.Items.Count.ShouldBe(1);

        var byTag = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Tags.Contains("how-to"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byTag.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Can_save_and_find_article_by_indexed_search_fields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var article = new ArticleDso.V1(
            "Configuring Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ["storage", "how-to"],
            new Dictionary<string, string>());

        // begin-snippet: search-fields-write-and-find
        var id = UuidV7.New();
        _ = await storage.CreateAsync(id, article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);

        // Case-insensitive: the stored value is "Ada Lovelace", the query uses different casing.
        var filter = Fields.Author.Equals("ADA LOVELACE");
        var result = await storage.QueryAsync(
            ArticleDso.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        ToArticle(result.Items[0].Value).Title.ShouldBe(article.Title);
    }

    [Fact]
    public async Task Updating_an_article_must_resupply_its_search_fields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var original = new ArticleDso.V1(
            "Draft title",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ["draft"],
            new Dictionary<string, string>());
        _ = await storage.CreateAsync(id, original, [], BuildSearchFields(original), Expiration.NoExpiration, [], ct);

        var updated = original with { Title = "Published title", Tags = ["storage", "how-to"] };

        // begin-snippet: update-search-fields
        // Pass the version from the latest read so a concurrent update is detected.
        var current = await storage.TryReadAsync(ArticleDso.EntityType, id, ct);

        // Search fields are not merged with what was written before: UpdateAsync replaces the
        // entity's entire set of search fields with whatever SearchFieldCollection is supplied here.
        // Omitting a previously-indexed field removes it from the search index.
        var updateResult = await storage.UpdateAsync(
            id, updated, current.Version!.Value, [], BuildSearchFields(updated), expiration: null, outboxEvents: [], ct);
        // end-snippet

        updateResult.ShouldBe(UpdateResult.Success);

        var byNewTitle = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Title.Contains("published"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byNewTitle.Items.Count.ShouldBe(1);

        var byOldTitle = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Title.Contains("draft"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byOldTitle.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Omitting_a_previously_indexed_field_on_update_removes_it_from_the_search_index()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var original = new ArticleDso.V1(
            "Draft title",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ["draft"],
            new Dictionary<string, string>());
        _ = await storage.CreateAsync(id, original, [], BuildSearchFields(original), Expiration.NoExpiration, [], ct);

        // Resupply only Title and PublishedAt: Author is deliberately omitted from the
        // search fields, even though the document still has it.
        var updated = original with { Title = "Published title" };
        var searchFieldsWithoutAuthor = new SearchFieldsBuilder()
            .Add(Fields.Title.Path, updated.Title)
            .Add(Fields.PublishedAt.Path, updated.PublishedAt)
            .Build();
        var updateResult = await storage.UpdateAsync(
            id, updated, 1, [], searchFieldsWithoutAuthor, expiration: null, outboxEvents: [], ct);
        updateResult.ShouldBe(UpdateResult.Success);

        // The old value is no longer queryable on that path ...
        var byOldAuthor = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Author.Equals("Ada Lovelace"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byOldAuthor.Items.Count.ShouldBe(0);

        // ... and neither is any other value on that path: the field has no row at all now.
        var byAnyAuthor = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Author.In(["Ada Lovelace", "Someone Else"]), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byAnyAuthor.Items.Count.ShouldBe(0);

        // The document itself still has the value; only the search index lost it.
        var read = await storage.TryReadAsync(ArticleDso.EntityType, id, ct);
        read.Found.ShouldBeTrue();
        ((ArticleDso.V1)read.Dso).Author.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task Only_indexed_dynamic_values_are_searchable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var article = new ArticleDso.V1(
            "Scaling dynamic attributes",
            "Grace Hopper",
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            ["storage"],
            new Dictionary<string, string>
            {
                ["priority"] = "high",
                // "internalNotes" is a customer-defined value too, but the product never
                // indexes it, so it stays readable in the document but cannot be queried.
                ["internalNotes"] = "needs technical review before publishing"
            });

        var id = UuidV7.New();

        // begin-snippet: dynamic-search-fields
        var builder = new SearchFieldsBuilder()
            .Add(Fields.Title.Path, article.Title)
            .Add(Fields.PublishedAt.Path, article.PublishedAt);
        _ = builder.AddExactMatch(Fields.Author.Path, article.Author);

        var tagIndex = 0;
        foreach (var tag in article.Tags)
        {
            _ = builder.Add(Fields.Tags.Path, tagIndex++, tag);
        }

        // article.CustomFields holds every customer-defined value, indexed or not.
        // Storage does not inspect the document, so the product decides which of these
        // dynamic values are worth indexing and writes only those into the search fields.
        // Here, only "priority" is searchable; other custom fields stay in the document only.
        if (article.CustomFields.TryGetValue("priority", out var priority))
        {
            _ = builder.Add("customFields.priority", priority);
        }

        var searchFields = builder.Build();
        // end-snippet

        var createResult = await storage.CreateAsync(
            id, article, [], searchFields, Expiration.NoExpiration, [], ct);
        createResult.ShouldBe(CreateResult.Success);

        // begin-snippet: dynamic-search-fields-query
        var byIndexedValue = new StringField("customFields.priority").Equals("high");
        var indexedResult = await storage.QueryAsync(
            ArticleDso.EntityType, byIndexedValue, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet
        indexedResult.Items.Count.ShouldBe(1);

        // The document itself still has every dynamic value, indexed or not.
        var read = await storage.TryReadAsync(ArticleDso.EntityType, id, ct);
        read.Found.ShouldBeTrue();
        ((ArticleDso.V1)read.Dso).CustomFields["internalNotes"].ShouldBe("needs technical review before publishing");

        // The un-indexed value is not queryable: there's no search row for it at all.
        var byUnindexedValue = new StringField("customFields.internalNotes").Contains("technical review");
        var unindexedResult = await storage.QueryAsync(
            ArticleDso.EntityType, byUnindexedValue, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        unindexedResult.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Filter_expressions_combine_with_and_or_and_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var matching = new ArticleDso.V1(
            "Configuring Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ["storage", "how-to"],
            new Dictionary<string, string>());
        var tooOld = new ArticleDso.V1(
            "Configuring Duende Storage (legacy)",
            "Ada Lovelace",
            new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ["storage"],
            new Dictionary<string, string>());
        var wrongAuthor = matching with { Author = "Someone Else" };

        foreach (var article in new[] { matching, tooOld, wrongAuthor })
        {
            _ = await storage.CreateAsync(
                UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        }

        // begin-snippet: filter-query
        var filter = Fields.PublishedAt.GreaterOrEqual(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .And(Fields.Title.StartsWith("Configuring").Or(Fields.Tags.Contains("how-to")))
            .And(Fields.Author.In(["Ada Lovelace", "Grace Hopper"]));

        var result = await storage.QueryAsync(
            ArticleDso.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        ToArticle(result.Items[0].Value).Title.ShouldBe(matching.Title);

        var count = await storage.CountAsync(ArticleDso.EntityType, Fields.Author.Equals("Ada Lovelace"), ct);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task Continuation_token_paging_walks_every_page_in_sort_order()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        for (var i = 0; i < 5; i++)
        {
            var article = new ArticleDso.V1(
                $"Article {i}",
                "Ada Lovelace",
                new DateTimeOffset(2026, 1, 1 + i, 0, 0, 0, TimeSpan.Zero),
                [],
                new Dictionary<string, string>());
            _ = await storage.CreateAsync(
                UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        }

        // begin-snippet: sort-and-page
        var titles = new List<string>();
        var sort = new SortParameter(Fields.PublishedAt);
        var range = DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 2);

        while (true)
        {
            var page = await storage.QueryAsync(
                ArticleDso.EntityType, Query.All(), sort, range, ct);
            titles.AddRange(page.Items.Select(item => ToArticle(item.Value).Title));

            if (!page.HasMoreData || page.NextToken is not { } nextToken)
            {
                break;
            }

            range = DataRange.FromContinuationToken(nextToken, size: 2);
        }
        // end-snippet

        titles.ShouldBe(["Article 0", "Article 1", "Article 2", "Article 3", "Article 4"]);
    }

    [Fact]
    public async Task Continuation_token_paging_works_without_a_sort()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var article = new ArticleDso.V1(
                $"Article {i}",
                "Ada Lovelace",
                new DateTimeOffset(2026, 1, 1 + i, 0, 0, 0, TimeSpan.Zero),
                [],
                new Dictionary<string, string>());
            var id = UuidV7.New();
            ids.Add(id.Value);
            _ = await storage.CreateAsync(id, article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        }

        // Without a sort, cursor paging falls back to a stable default order (entity id),
        // the same order unsorted offset paging uses, rather than throwing.
        var range = DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 2);
        var seen = new List<Guid>();
        while (true)
        {
            var page = await storage.QueryAsync(
                ArticleDso.EntityType, Query.All(), SortParameter.Empty, range, ct);
            seen.AddRange(page.Items.Select(item => item.Id));

            if (!page.HasMoreData || page.NextToken is not { } nextToken)
            {
                break;
            }

            range = DataRange.FromContinuationToken(nextToken, size: 2);
        }

        seen.ShouldBe(ids.OrderBy(id => id.ToString()).ToList());
    }

    [Fact]
    public async Task PreviousToken_navigates_backward_with_the_same_sort()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        for (var i = 0; i < 6; i++)
        {
            var article = new ArticleDso.V1(
                $"Article {i}",
                "Ada Lovelace",
                new DateTimeOffset(2026, 1, 1 + i, 0, 0, 0, TimeSpan.Zero),
                [],
                new Dictionary<string, string>());
            _ = await storage.CreateAsync(
                UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        }

        var sort = new SortParameter(Fields.PublishedAt);

        var page1 = await storage.QueryAsync(
            ArticleDso.EntityType, Query.All(), sort,
            DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 2), ct);
        var page2 = await storage.QueryAsync(
            ArticleDso.EntityType, Query.All(), sort,
            DataRange.FromContinuationToken(page1.NextToken!.Value, size: 2), ct);

        // PreviousToken navigates backward using the SAME sort: no need to flip the direction.
        // Items always come back in the requested (forward) order.
        var previousToken = page2.PreviousToken.ShouldNotBeNull();
        var backToPage1 = await storage.QueryAsync(
            ArticleDso.EntityType, Query.All(), sort,
            DataRange.FromContinuationToken(previousToken.Value, size: 2), ct);

        backToPage1.Items.Select(i => ToArticle(i.Value).Title).ShouldBe(
            page1.Items.Select(i => ToArticle(i.Value).Title).ToList());
    }

    // begin-snippet: page-backward
    private static async Task<(List<string> Forward, List<string> Backward)> PageForwardThenBackwardAsync(
        IPartitionedStorage storage, Ct ct)
    {
        var sort = new SortParameter(Fields.PublishedAt);

        // Page forward to the last page.
        var forwardTitles = new List<string>();
        var range = DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 2);
        QueryResult<MetadataEnvelope> page;
        while (true)
        {
            page = await storage.QueryAsync(ArticleDso.EntityType, Query.All(), sort, range, ct);
            forwardTitles.AddRange(page.Items.Select(item => ToArticle(item.Value).Title));

            if (!page.HasMoreData || page.NextToken is not { } nextToken)
            {
                break;
            }

            range = DataRange.FromContinuationToken(nextToken, size: 2);
        }

        // Page backward from the last page using PreviousToken and the SAME sort.
        var backwardTitles = new List<string>(page.Items.Select(item => ToArticle(item.Value).Title));
        while (page.HasPreviousData && page.PreviousToken is { } previousToken)
        {
            page = await storage.QueryAsync(
                ArticleDso.EntityType, Query.All(), sort,
                DataRange.FromContinuationToken(previousToken, size: 2), ct);
            backwardTitles.InsertRange(0, page.Items.Select(item => ToArticle(item.Value).Title));
        }

        return (forwardTitles, backwardTitles);
    }
    // end-snippet

    [Fact]
    public async Task Paging_backward_from_the_last_page_reproduces_the_forward_pages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        for (var i = 0; i < 5; i++)
        {
            var article = new ArticleDso.V1(
                $"Article {i}",
                "Ada Lovelace",
                new DateTimeOffset(2026, 1, 1 + i, 0, 0, 0, TimeSpan.Zero),
                [],
                new Dictionary<string, string>());
            _ = await storage.CreateAsync(
                UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        }

        var (forward, backward) = await PageForwardThenBackwardAsync(storage, ct);

        forward.ShouldBe(["Article 0", "Article 1", "Article 2", "Article 3", "Article 4"]);
        backward.ShouldBe(forward);
    }

    [Fact]
    public async Task StringArrayField_supports_filtering_but_not_sorting_or_projection()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var article = new ArticleDso.V1(
            "Configuring Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ["storage", "how-to"],
            new Dictionary<string, string>());
        _ = await storage.CreateAsync(
            UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);

        // Filtering works: Contains matches an item in the array.
        var filtered = await storage.QueryAsync(
            ArticleDso.EntityType, Fields.Tags.Contains("storage"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        filtered.Items.Count.ShouldBe(1);

        // Sorting by a multi-valued field is rejected: there's no single row per entity to sort by.
        var sortByTags = new SortParameter(Fields.Tags);
        _ = await Should.ThrowAsync<Exception>(() => storage.QueryAsync(
            ArticleDso.EntityType, Query.All(), sortByTags, DataRange.FromPage(1, 10), ct));

        // Projecting a multi-valued field excludes its rows: QueryFieldsAsync only reads the
        // single-value (item_index = -1) row, which a StringArrayField never writes.
        var projected = await storage.QueryFieldsAsync(
            ArticleDso.EntityType, new List<Field> { Fields.Title, Fields.Tags }, Query.All(), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        projected.Items.Count.ShouldBe(1);
        projected.Items[0].Fields.ShouldContainKey("TAGS");
        projected.Items[0].Fields["TAGS"].ShouldBeNull();
    }

    // begin-snippet: typed-query-types
    public sealed record ArticleFilter
    {
        public string? TitleContains { get; init; }
        public string? Author { get; init; }
    }

    public enum ArticleSortField
    {
        Title = 1,
        PublishedAt = 2
    }
    // end-snippet

    [Fact]
    public async Task Typed_filter_and_sort_translate_to_search_fields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var earlierByAda = new ArticleDso.V1(
            "Configuring Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            [],
            new Dictionary<string, string>());
        var laterByAda = new ArticleDso.V1(
            "Extending Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero),
            [],
            new Dictionary<string, string>());
        var byGrace = new ArticleDso.V1(
            "Extending Duende Storage",
            "Grace Hopper",
            new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            [],
            new Dictionary<string, string>());

        foreach (var article in new[] { earlierByAda, laterByAda, byGrace })
        {
            _ = await storage.CreateAsync(
                UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        }

        var request = QueryRequest.Create<ArticleFilter, ArticleSortField>(
            new ArticleFilter { Author = "Ada Lovelace" },
            SortBy.Field(ArticleSortField.Title));

        // begin-snippet: typed-query
        var filterValue = request.Filter?.FilterValue;
        IQueryFilterExpression? expression = null;
        if (filterValue is not null)
        {
            if (!string.IsNullOrWhiteSpace(filterValue.TitleContains))
            {
                expression = Fields.Title.Contains(filterValue.TitleContains);
            }

            if (!string.IsNullOrWhiteSpace(filterValue.Author))
            {
                var authorExpression = Fields.Author.Equals(filterValue.Author);
                expression = expression is null ? authorExpression : expression.And(authorExpression);
            }
        }

        var filter = expression ?? AllExpression.Instance;

        var sortRequest = request.Sort;
        Field sortField = Fields.PublishedAt;
        var sortDirection = SortDirection.Ascending;
        if (sortRequest is not null)
        {
            sortField = sortRequest.Field switch
            {
                ArticleSortField.Title => Fields.Title,
                ArticleSortField.PublishedAt => Fields.PublishedAt,
                _ => throw new ArgumentOutOfRangeException(nameof(request), sortRequest.Field, "Unknown sort field")
            };
            sortDirection = sortRequest.Direction;
        }

        var sort = new SortParameter(sortField, sortDirection);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var queryResult = await storage.QueryAsync(ArticleDso.EntityType, filter, sort, range, ct);
        var result = queryResult.ConvertTo(envelope => ToArticle(envelope.Value));
        // end-snippet

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(a => a.Author == "Ada Lovelace");
        // Default direction is ascending, and "Configuring..." sorts before "Extending...".
        result.Items[0].Title.ShouldBe(earlierByAda.Title);
        result.Items[1].Title.ShouldBe(laterByAda.Title);
    }

    [Fact]
    public async Task Field_projection_returns_only_the_requested_search_values()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var article = new ArticleDso.V1(
            "Configuring Duende Storage",
            "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ["storage"],
            new Dictionary<string, string>());
        _ = await storage.CreateAsync(
            UuidV7.New(), article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);

        // begin-snippet: field-projection
        // Only fields that were written to the search index can be projected: field-projection
        // queries read straight from the search-values rows and never deserialize the document.
        // ExactMatchField values (like Fields.Author) project the stored hash, not the original
        // string, so they're rarely useful to include here.
        var fields = new List<Field> { Fields.Title, Fields.PublishedAt, Fields.Author };

        var result = await storage.QueryFieldsAsync(
            ArticleDso.EntityType, fields, Query.All(), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        // Projected string values are the normalized (upper-invariant) search representation,
        // not the original casing kept in the stored document.
        result.Items[0].Fields["TITLE"].ShouldBe(article.Title.ToUpperInvariant());
        // ExactMatchField projects the stored deterministic hash, not the original string.
        var expectedAuthorHash = DeterministicGuidGenerator.Create(article.Author.ToUpperInvariant());
        result.Items[0].Fields["AUTHOR"].ShouldBe(expectedAuthorHash);
        // A field that was requested but never written as a search value is present with a
        // null value, it is not absent from the dictionary.
        var unindexedField = new StringField("customFields.priority");
        var withUnindexedField = await storage.QueryFieldsAsync(
            ArticleDso.EntityType,
            new List<Field> { Fields.Title, unindexedField },
            Query.All(),
            SortParameter.Empty,
            DataRange.FromPage(1, 10),
            ct);
        withUnindexedField.Items.Count.ShouldBe(1);
        withUnindexedField.Items[0].Fields.ShouldContainKey("CUSTOMFIELDS.PRIORITY");
        withUnindexedField.Items[0].Fields["CUSTOMFIELDS.PRIORITY"].ShouldBeNull();
    }

    // begin-snippet: link-filter-publisher-dso
    public static class PublisherDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(9002, nameof(PublisherDso));

        public sealed record V1(string Name) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }

    public static class PublisherLinks
    {
        // Illustrative id only. Real link types must use an unused id from the owning product's reserved range.
        public static readonly LinkDefinition Publishes = new()
        {
            Left = PublisherDso.EntityType,
            Right = ArticleDso.EntityType,
            Link = new LinkType(9201, "Publishes")
        };
    }

    private static class PublisherFields
    {
        public static readonly StringField Name = new("name");
    }
    // end-snippet

    [Fact]
    public async Task Can_filter_articles_by_their_publisher()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<ArticleDso.V1>();
                services.AddDsoRegistration<PublisherDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmePress = UuidV7.New();
        var othersPress = UuidV7.New();
        _ = await storage.CreateAsync(acmePress, new PublisherDso.V1("Acme Press"), [],
            new SearchFieldsBuilder().Add(PublisherFields.Name.Path, "Acme Press").Build(), Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(othersPress, new PublisherDso.V1("Others Press"), [],
            new SearchFieldsBuilder().Add(PublisherFields.Name.Path, "Others Press").Build(), Expiration.NoExpiration, [], ct);

        var article = new ArticleDso.V1(
            "Configuring Duende Storage", "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), ["storage"], new Dictionary<string, string>());
        var otherArticle = new ArticleDso.V1(
            "Something Else", "Grace Hopper",
            new DateTimeOffset(2026, 1, 16, 0, 0, 0, TimeSpan.Zero), [], new Dictionary<string, string>());

        var articleId = UuidV7.New();
        var otherArticleId = UuidV7.New();
        _ = await storage.CreateAsync(articleId, article, [], BuildSearchFields(article), Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(otherArticleId, otherArticle, [], BuildSearchFields(otherArticle), Expiration.NoExpiration, [], ct);

        _ = await storage.LinkAsync(PublisherLinks.Publishes, acmePress, articleId, [], ct);
        _ = await storage.LinkAsync(PublisherLinks.Publishes, othersPress, otherArticleId, [], ct);

        // begin-snippet: link-filter-to-left
        // "Which articles does Acme Press publish?" (the queried entity, Article, is the
        // right side of Publishes, so the direction is ToLeft.)
        var filter = LinkFilter.ToLeft(PublisherLinks.Publishes, PublisherFields.Name.Equals("Acme Press"));

        var result = await storage.QueryAsync(
            ArticleDso.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        ((ArticleDso.V1)result.Items[0].Value).Title.ShouldBe(article.Title);

        // begin-snippet: link-filter-to-right
        // "Which publishers published the article titled 'Configuring Duende Storage'?"
        // (the queried entity, Publisher, is the left side of Publishes, so the direction is ToRight.)
        var publisherFilter = LinkFilter.ToRight(PublisherLinks.Publishes, Fields.Title.Equals("Configuring Duende Storage"));

        var publisherResult = await storage.QueryAsync(
            PublisherDso.EntityType, publisherFilter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        publisherResult.Items.Count.ShouldBe(1);
        ((PublisherDso.V1)publisherResult.Items[0].Value).Name.ShouldBe("Acme Press");
    }

    [Fact]
    public async Task Link_filters_combine_with_other_filters_Not_sort_and_paging()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<ArticleDso.V1>();
                services.AddDsoRegistration<PublisherDso.V1>();
            });
        var storage = fixture.PartitionedStorage;

        var acmePress = UuidV7.New();
        _ = await storage.CreateAsync(acmePress, new PublisherDso.V1("Acme Press"), [],
            new SearchFieldsBuilder().Add(PublisherFields.Name.Path, "Acme Press").Build(), Expiration.NoExpiration, [], ct);

        var published = new ArticleDso.V1(
            "Configuring Duende Storage", "Ada Lovelace",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), [], new Dictionary<string, string>());
        var unpublished = new ArticleDso.V1(
            "Draft Article", "Ada Lovelace",
            new DateTimeOffset(2026, 1, 16, 0, 0, 0, TimeSpan.Zero), [], new Dictionary<string, string>());

        var publishedId = UuidV7.New();
        var unpublishedId = UuidV7.New();
        _ = await storage.CreateAsync(publishedId, published, [], BuildSearchFields(published), Expiration.NoExpiration, [], ct);
        _ = await storage.CreateAsync(unpublishedId, unpublished, [], BuildSearchFields(unpublished), Expiration.NoExpiration, [], ct);

        _ = await storage.LinkAsync(PublisherLinks.Publishes, acmePress, publishedId, [], ct);

        // begin-snippet: link-filter-and-not
        // Combine a link filter with an ordinary field filter: articles by Ada Lovelace
        // that a publisher has picked up.
        var byAuthorAndPublished = Fields.Author.Equals("Ada Lovelace")
            .And(LinkFilter.ToLeft(PublisherLinks.Publishes));

        var pickedUp = await storage.QueryAsync(
            ArticleDso.EntityType, byAuthorAndPublished, SortParameter.Empty, DataRange.FromPage(1, 10), ct);

        // Query.Not negates a link filter: articles no publisher has picked up yet.
        var stillDrafts = await storage.QueryAsync(
            ArticleDso.EntityType, Query.Not(LinkFilter.ToLeft(PublisherLinks.Publishes)),
            SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        pickedUp.Items.Count.ShouldBe(1);
        ((ArticleDso.V1)pickedUp.Items[0].Value).Title.ShouldBe(published.Title);
        stillDrafts.Items.Count.ShouldBe(1);
        ((ArticleDso.V1)stillDrafts.Items[0].Value).Title.ShouldBe(unpublished.Title);

        // begin-snippet: link-filter-sort-and-page
        // Sort and page results the same way as any other QueryAsync call; TotalCount
        // counts distinct matching articles, even if several publishers linked to the same one.
        var anyPublisher = LinkFilter.ToLeft(PublisherLinks.Publishes);
        var page = await storage.QueryAsync(
            ArticleDso.EntityType, anyPublisher, new SortParameter(Fields.PublishedAt), DataRange.FromPage(1, 10), ct);
        // end-snippet

        page.Items.Count.ShouldBe(1);
        page.TotalCount.ShouldBe(1);
    }
}
