// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using SortDirection = Duende.Storage.Querying.SortDirection;

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

        var byTitle = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Fields.Title.Contains("Configuring"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byTitle.Items.Count.ShouldBe(1);

        var byTag = await storage.QueryAsync<ArticleDso.V1>(
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
        var result = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Title.ShouldBe(article.Title);
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

        var byNewTitle = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Fields.Title.Contains("published"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byNewTitle.Items.Count.ShouldBe(1);

        var byOldTitle = await storage.QueryAsync<ArticleDso.V1>(
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
        var byOldAuthor = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Fields.Author.Equals("Ada Lovelace"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byOldAuthor.Items.Count.ShouldBe(0);

        // ... and neither is any other value on that path: the field has no row at all now.
        var byAnyAuthor = await storage.QueryAsync<ArticleDso.V1>(
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
        var indexedResult = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, byIndexedValue, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet
        indexedResult.Items.Count.ShouldBe(1);

        // The document itself still has every dynamic value, indexed or not.
        var read = await storage.TryReadAsync(ArticleDso.EntityType, id, ct);
        read.Found.ShouldBeTrue();
        ((ArticleDso.V1)read.Dso).CustomFields["internalNotes"].ShouldBe("needs technical review before publishing");

        // The un-indexed value is not queryable: there's no search row for it at all.
        var byUnindexedValue = new StringField("customFields.internalNotes").Contains("technical review");
        var unindexedResult = await storage.QueryAsync<ArticleDso.V1>(
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

        var result = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Title.ShouldBe(matching.Title);

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
            var page = await storage.QueryAsync<ArticleDso.V1>(
                ArticleDso.EntityType, Query.All(), sort, range, ct);
            titles.AddRange(page.Items.Select(item => item.Value.Title));

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
    public async Task Continuation_token_paging_requires_a_non_empty_sort()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ArticleDso.V1>());
        var storage = fixture.PartitionedStorage;

        var range = DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 2);

        _ = await Should.ThrowAsync<ArgumentException>(() => storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Query.All(), SortParameter.Empty, range, ct));
    }

    [Fact]
    public async Task PreviousToken_only_navigates_backward_when_the_sort_direction_is_reversed()
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

        var forwardSort = new SortParameter(Fields.PublishedAt);
        var backwardSort = new SortParameter(Fields.PublishedAt, SortDirection.Descending);

        var page1 = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Query.All(), forwardSort,
            DataRange.FromContinuationToken(ContinuationToken.Beginning, size: 2), ct);
        var page2 = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Query.All(), forwardSort,
            DataRange.FromContinuationToken(page1.NextToken!.Value, size: 2), ct);

        // Using PreviousToken with the SAME (forward) sort direction does not walk backward:
        // the sort direction must be reversed for PreviousToken to mean "the page before this one".
        var previousToken = page2.PreviousToken.ShouldNotBeNull();
        var withSameSortDirection = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Query.All(), forwardSort,
            DataRange.FromContinuationToken(previousToken.Value, size: 2), ct);
        withSameSortDirection.Items.Select(i => i.Value.Title).ShouldNotBe(
            page1.Items.Select(i => i.Value.Title).ToList());

        // Reversing the sort direction makes PreviousToken return the previous page's items
        // (in reverse order, matching the reversed sort).
        var withReversedSortDirection = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Query.All(), backwardSort,
            DataRange.FromContinuationToken(previousToken.Value, size: 2), ct);
        withReversedSortDirection.Items.Select(i => i.Value.Title).ShouldBe(
            page1.Items.Select(i => i.Value.Title).Reverse().ToList());
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
        var filtered = await storage.QueryAsync<ArticleDso.V1>(
            ArticleDso.EntityType, Fields.Tags.Contains("storage"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        filtered.Items.Count.ShouldBe(1);

        // Sorting by a multi-valued field is rejected: there's no single row per entity to sort by.
        var sortByTags = new SortParameter(Fields.Tags);
        _ = await Should.ThrowAsync<Exception>(() => storage.QueryAsync<ArticleDso.V1>(
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

        var queryResult = await storage.QueryAsync<ArticleDso.V1>(ArticleDso.EntityType, filter, sort, range, ct);
        var result = queryResult.ConvertTo(envelope => envelope.Value);
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
}
