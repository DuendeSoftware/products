// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Pagination;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.Sqlite;

/// <summary>
/// SQLite stores a decimal "rating" search field as a REAL (double). Values at or beyond
/// <see cref="decimal.MaxValue"/>/<see cref="decimal.MinValue"/> are read back by clamping,
/// since the double nearest those bounds can otherwise overflow <c>decimal.Parse</c>
/// (Design decision: C6 review fix).
/// </summary>
public sealed class SqliteDecimalBoundaryCursorTests
{
    private readonly EntityType _entityType = TestSortDso.DsoVersion.EntityType;

    private static Task<IStorageFixture> CreateFixtureAsync(Ct ct) =>
        new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services => services.AddDsoRegistration<TestSortDso>());

    private static async Task<Guid> CreateRatingEntityAsync(IPartitionedStorage partitionedStorage, string name, decimal rating, Ct ct)
    {
        var id = UuidV7.New();
        var dso = new TestSortDso { Name = name, Rating = rating };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        _ = searchFieldsBuilder.Add("rating", rating);

        var result = await partitionedStorage.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFieldsBuilder.Build(), Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id.Value;
    }

    [Fact]
    public async Task Decimal_boundary_rating_values_traverse_forward_and_backward_without_repeats_or_gaps()
    {
        // Arrange - decimal.MaxValue/MinValue and near-boundary neighbours, plus zero, page size 1.
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var partitionedStorage = fixture.PartitionedStorage;

        var ratings = new[]
        {
            decimal.MinValue,
            decimal.MinValue + 1e15m,
            0m,
            decimal.MaxValue - 1e15m,
            decimal.MaxValue,
        };

        var ids = new List<Guid>();
        foreach (var rating in ratings)
        {
            ids.Add(await CreateRatingEntityAsync(partitionedStorage, $"Product-{rating}", rating, ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rating"));

        // Act - page forward to the end, one row at a time.
        var forwardIds = new List<Guid>();
        ContinuationToken token = ContinuationToken.Beginning;
        QueryResult<MetadataEnvelope> page;
        while (true)
        {
            page = await partitionedStorage.QueryAsync(
                _entityType, filter, sort, DataRange.FromContinuationToken(token, 1), ct);
            forwardIds.AddRange(page.Items.Select(i => i.Id));
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        // Assert - every record exactly once, forward.
        forwardIds.Count.ShouldBe(ids.Count);
        forwardIds.ShouldBe(ids, ignoreOrder: true);
        forwardIds.Distinct().Count().ShouldBe(ids.Count);

        // Act - page backward from the last page using PreviousToken and the SAME sort.
        var backwardIds = new List<Guid>(page.Items.Select(i => i.Id));
        while (page.HasPreviousData && page.PreviousToken is { } previousToken)
        {
            page = await partitionedStorage.QueryAsync(
                _entityType, filter, sort, DataRange.FromContinuationToken(previousToken, 1), ct);
            backwardIds.InsertRange(0, page.Items.Select(i => i.Id));
        }

        // Assert - backward pages equal the forward pages, reversed.
        backwardIds.ShouldBe(forwardIds);
    }

    [Fact]
    public async Task Small_scale_28_decimal_rating_values_including_duplicates_traverse_forward_and_backward_without_repeats_or_gaps()
    {
        // Arrange - tiny decimals at scale 28, including a pair of exact duplicates, page size 1.
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var partitionedStorage = fixture.PartitionedStorage;

        var ratings = new[]
        {
            0.0000000000002728515301765674m,
            0.0000000000002728515301765674m,
            0.0000000000002728515301765675m,
            0.0000000000002728515301765673m,
            0.0000000000000000000000012345m,
            0.1234567890123456789012345678m,
        };

        var ids = new List<Guid>();
        var index = 0;
        foreach (var rating in ratings)
        {
            ids.Add(await CreateRatingEntityAsync(partitionedStorage, $"Product-{index++}", rating, ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rating"));
        const int maxIterations = 50;

        // Act - page forward to the end, one row at a time.
        var forwardIds = new List<Guid>();
        ContinuationToken token = ContinuationToken.Beginning;
        QueryResult<MetadataEnvelope> page;
        var iterations = 0;
        while (true)
        {
            iterations++.ShouldBeLessThan(maxIterations, "the forward traversal did not terminate");
            page = await partitionedStorage.QueryAsync(
                _entityType, filter, sort, DataRange.FromContinuationToken(token, 1), ct);
            forwardIds.AddRange(page.Items.Select(i => i.Id));
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        // Assert - every record exactly once, forward.
        forwardIds.Count.ShouldBe(ids.Count);
        forwardIds.ShouldBe(ids, ignoreOrder: true);
        forwardIds.Distinct().Count().ShouldBe(ids.Count);

        // Act - page backward from the last page using PreviousToken and the SAME sort.
        var backwardIds = new List<Guid>(page.Items.Select(i => i.Id));
        iterations = 0;
        while (page.HasPreviousData && page.PreviousToken is { } previousToken)
        {
            iterations++.ShouldBeLessThan(maxIterations, "the backward traversal did not terminate");
            page = await partitionedStorage.QueryAsync(
                _entityType, filter, sort, DataRange.FromContinuationToken(previousToken, 1), ct);
            backwardIds.InsertRange(0, page.Items.Select(i => i.Id));
        }

        // Assert - backward pages equal the forward pages, reversed.
        backwardIds.ShouldBe(forwardIds);
    }

    [Fact]
    public void Bind_path_round_trips_decimal_MaxValue_and_MinValue_through_the_same_double_conversion()
    {
        // The bind path (BindCursorPositionParameters) converts a decimal cursor value to a double via
        // double.Parse(decimal.ToString(invariant)), the same conversion SQLite applies when storing a
        // decimal search field value as REAL. If MaxValue/MinValue didn't round-trip through that same
        // conversion, a cursor built from a stored boundary value wouldn't match the stored REAL.
        var maxAsDouble = double.Parse(decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
        var minAsDouble = double.Parse(decimal.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);

        maxAsDouble.ShouldBe((double)decimal.MaxValue);
        minAsDouble.ShouldBe((double)decimal.MinValue);
    }
}
