// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;
using SortDirection = Duende.Storage.Querying.SortDirection;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Cross-store integration tests for filtering and sorting by the system timestamp
/// fields (created, last_updated). These fields bypass the search_values EAV path
/// and use entity-level columns directly.
/// </summary>
public partial class SystemTimestampQueryTests
{

    private readonly EntityType _testEntityType = new(3, "TestEntity");
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStoreFixture> CreateProviderAsync(FakeTimeProvider tp) =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton(tp);
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<TestEntityDso>();
        });

    private static async Task<UuidV7> CreateEntityAsync(IStore store, string name, Ct ct)
    {
        var id = UuidV7.New();
        var dso = new TestEntityDso { Name = name };
        (await store.CreateAsync(id, dso, [], [], Expiration.NoExpiration, [], ct)).ShouldBe(CreateResult.Success);
        return id;
    }

    // ── Filtering by created ────────────────────────────────────────────

    [Fact]
    public async Task Filter_by_created_gt_returns_entities_created_after_cutoff()
    {
        // Arrange
        var jan = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var jun = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var dec = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(jan);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        _ = await CreateEntityAsync(store, "Jan", _ct);
        tp.SetUtcNow(jun);
        _ = await CreateEntityAsync(store, "Jun", _ct);
        tp.SetUtcNow(dec);
        _ = await CreateEntityAsync(store, "Dec", _ct);

        var filter = SystemFields.CreatedAtField.GreaterThan(jun);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Dec");
    }

    [Fact]
    public async Task Filter_by_created_between_returns_entities_in_range()
    {
        // Arrange
        var jan = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var mar = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var sep = new DateTimeOffset(2025, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var dec = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);

        var feb = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var oct = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(jan);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        _ = await CreateEntityAsync(store, "Jan", _ct);
        tp.SetUtcNow(mar);
        _ = await CreateEntityAsync(store, "Mar", _ct);
        tp.SetUtcNow(sep);
        _ = await CreateEntityAsync(store, "Sep", _ct);
        tp.SetUtcNow(dec);
        _ = await CreateEntityAsync(store, "Dec", _ct);

        var filter = SystemFields.CreatedAtField.Between(feb, oct);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Mar");
        result.Items.ShouldContain(x => x.Value.Name == "Sep");
    }

    [Fact]
    public async Task Filter_by_created_eq_returns_exact_match()
    {
        // Arrange
        var t1 = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(t1);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        _ = await CreateEntityAsync(store, "First", _ct);
        tp.SetUtcNow(t2);
        _ = await CreateEntityAsync(store, "Second", _ct);
        tp.SetUtcNow(t3);
        _ = await CreateEntityAsync(store, "Third", _ct);

        var filter = SystemFields.CreatedAtField.Equals(t2);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Second");
    }

    // ── Filtering by last_updated ───────────────────────────────────────

    [Fact]
    public async Task Filter_by_last_updated_gt_returns_recently_updated_entities()
    {
        // Arrange
        var t1 = new DateTimeOffset(2025, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var midpoint = new DateTimeOffset(2025, 3, 1, 11, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(t1);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var idA = await CreateEntityAsync(store, "EntityA", _ct);
        var idB = await CreateEntityAsync(store, "EntityB", _ct);

        // Advance clock and update only EntityB
        tp.SetUtcNow(t2);
        var readResult = await store.TryReadAsync(_testEntityType, idB, _ct);
        readResult.Found.ShouldBeTrue();
        var updatedDso = new TestEntityDso { Name = "EntityB-updated" };
        (await store.UpdateAsync(idB, updatedDso, readResult.Version!.Value, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(UpdateResult.Success);

        var filter = SystemFields.LastUpdatedAtField.GreaterThan(midpoint);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("EntityB-updated");
    }

    // ── Sorting by created ──────────────────────────────────────────────

    [Fact]
    public async Task Sort_by_created_ascending_returns_oldest_first()
    {
        // Arrange — create in chronological order but with non-alphabetical names
        var t1 = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(t1);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        _ = await CreateEntityAsync(store, "Charlie", _ct);
        tp.SetUtcNow(t2);
        _ = await CreateEntityAsync(store, "Alpha", _ct);
        tp.SetUtcNow(t3);
        _ = await CreateEntityAsync(store, "Bravo", _ct);

        var sort = new SortParameter(SystemFields.CreatedAtField);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, Query.All(), sort, page, Ct.None);

        // Assert — oldest first: Charlie(t1), Alpha(t2), Bravo(t3)
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("Charlie");
        result.Items[1].Value.Name.ShouldBe("Alpha");
        result.Items[2].Value.Name.ShouldBe("Bravo");
    }

    [Fact]
    public async Task Sort_by_created_descending_returns_newest_first()
    {
        // Arrange — create in chronological order but with non-alphabetical names
        var t1 = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(t1);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        _ = await CreateEntityAsync(store, "Charlie", _ct);
        tp.SetUtcNow(t2);
        _ = await CreateEntityAsync(store, "Alpha", _ct);
        tp.SetUtcNow(t3);
        _ = await CreateEntityAsync(store, "Bravo", _ct);

        var sort = new SortParameter(SystemFields.CreatedAtField, SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, Query.All(), sort, page, Ct.None);

        // Assert — newest first: Bravo(t3), Alpha(t2), Charlie(t1)
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("Bravo");
        result.Items[1].Value.Name.ShouldBe("Alpha");
        result.Items[2].Value.Name.ShouldBe("Charlie");
    }

    // ── Sorting by last_updated ─────────────────────────────────────────

    [Fact]
    public async Task Sort_by_last_updated_ascending_returns_least_recently_updated_first()
    {
        // Arrange: create 3 entities at t0, then update them in order C, A, B
        var t0 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(t0);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var idA = await CreateEntityAsync(store, "A", _ct);
        var idB = await CreateEntityAsync(store, "B", _ct);
        var idC = await CreateEntityAsync(store, "C", _ct);

        // Update C at t1
        tp.SetUtcNow(t1);
        var readC = await store.TryReadAsync(_testEntityType, idC, _ct);
        (await store.UpdateAsync(idC, new TestEntityDso { Name = "C" }, readC.Version!.Value, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(UpdateResult.Success);

        // Update A at t2
        tp.SetUtcNow(t2);
        var readA = await store.TryReadAsync(_testEntityType, idA, _ct);
        (await store.UpdateAsync(idA, new TestEntityDso { Name = "A" }, readA.Version!.Value, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(UpdateResult.Success);

        // Update B at t3
        tp.SetUtcNow(t3);
        var readB = await store.TryReadAsync(_testEntityType, idB, _ct);
        (await store.UpdateAsync(idB, new TestEntityDso { Name = "B" }, readB.Version!.Value, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(UpdateResult.Success);

        var sort = new SortParameter(SystemFields.LastUpdatedAtField);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, Query.All(), sort, page, Ct.None);

        // Assert: C(t1) < A(t2) < B(t3)
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("C");
        result.Items[1].Value.Name.ShouldBe("A");
        result.Items[2].Value.Name.ShouldBe("B");
    }

    // ── Combined filter + sort ──────────────────────────────────────────

    [Fact]
    public async Task Filter_by_created_gte_and_sort_by_created_descending()
    {
        // Arrange
        var jan = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var mar = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var sep = new DateTimeOffset(2025, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var dec = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(jan);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        _ = await CreateEntityAsync(store, "Jan", _ct);
        tp.SetUtcNow(mar);
        _ = await CreateEntityAsync(store, "Mar", _ct);
        tp.SetUtcNow(sep);
        _ = await CreateEntityAsync(store, "Sep", _ct);
        tp.SetUtcNow(dec);
        _ = await CreateEntityAsync(store, "Dec", _ct);

        // Filter: created >= Mar, Sort: descending
        var filter = SystemFields.CreatedAtField.GreaterOrEqual(mar);
        var sort = new SortParameter(SystemFields.CreatedAtField, SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert: Dec, Sep, Mar (all >= mar, newest first)
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("Dec");
        result.Items[1].Value.Name.ShouldBe("Sep");
        result.Items[2].Value.Name.ShouldBe("Mar");
    }

    // ── Projection of system fields ─────────────────────────────────────

    [Fact]
    public async Task Project_created_and_last_updated_via_public_alias()
    {
        // Arrange — use the public alias form ("created_at", "last_updated_at") to verify
        // that Field.Path uppercasing doesn't break projection lookups.
        var t1 = new DateTimeOffset(2025, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);

        var tp = new FakeTimeProvider(t1);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var id = await CreateEntityAsync(store, "Entity1", _ct);

        // Update to advance last_updated
        tp.SetUtcNow(t2);
        var readResult = await store.TryReadAsync(_testEntityType, id, _ct);
        readResult.Found.ShouldBeTrue();
        (await store.UpdateAsync(id, new TestEntityDso { Name = "Entity1-updated" }, readResult.Version!.Value, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(UpdateResult.Success);

        // Project using public alias names
        var fields = new Field[]
        {
            new DateTimeField(SystemFields.CreatedAttributeName),
            new DateTimeField(SystemFields.LastUpdatedAttributeName)
        };
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await queryStore.QueryFieldsAsync(_testEntityType, fields, Query.All(), SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        var projected = result.Items[0];
        var createdKey = fields[0].Path; // uppercased by Field.Path
        var lastUpdatedKey = fields[1].Path;

        projected.Fields.ShouldContainKey(createdKey);
        projected.Fields.ShouldContainKey(lastUpdatedKey);

        var createdValue = projected.Fields[createdKey].ShouldBeOfType<DateTimeOffset>();
        var lastUpdatedValue = projected.Fields[lastUpdatedKey].ShouldBeOfType<DateTimeOffset>();

        createdValue.ShouldBe(t1);
        lastUpdatedValue.ShouldBe(t2);
    }
}
