// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Tests for IPartitionedStorage.CountAsync across all storage implementations.
/// </summary>
public partial class QueryStoreCountTests
{
    private readonly EntityType _testEntityType = new(3, "TestEntity");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStorageFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services => services.AddDsoRegistration<TestEntityDso>());

    private static async Task CreateEntityAsync(IPartitionedStorage partitionedStorage, string name, int? score = null, Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestEntityDso { Name = name, Score = score };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        if (score.HasValue)
        {
            _ = searchFieldsBuilder.Add("score", score.Value);
        }

        var searchFields = searchFieldsBuilder.Build();

        var result = await partitionedStorage.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
    }

    [Fact]
    public async Task CountAsync_with_no_filter_should_return_total_count()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        await CreateEntityAsync(partitionedStorage, "Alice", ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Bob", ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Charlie", ct: _ct);

        // Act
        var count = await partitionedStorage.CountAsync(_testEntityType, null, _ct);

        // Assert
        count.ShouldBe(3);
    }

    [Fact]
    public async Task CountAsync_with_AllExpression_should_return_total_count()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        await CreateEntityAsync(partitionedStorage, "Alice", ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Bob", ct: _ct);

        // Act
        var count = await partitionedStorage.CountAsync(_testEntityType, AllExpression.Instance, _ct);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task CountAsync_with_filter_should_return_matching_count()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        await CreateEntityAsync(partitionedStorage, "Alice", score: 100, ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Bob", score: 50, ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Charlie", score: 100, ct: _ct);

        var filter = new NumberField("score").Equals(100);

        // Act
        var count = await partitionedStorage.CountAsync(_testEntityType, filter, _ct);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task CountAsync_with_no_matching_entities_should_return_zero()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        await CreateEntityAsync(partitionedStorage, "Alice", ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Bob", ct: _ct);

        var filter = new StringField("name").Equals("NonExistent");

        // Act
        var count = await partitionedStorage.CountAsync(_testEntityType, filter, _ct);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task CountAsync_with_empty_storage_should_return_zero()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        // Act
        var count = await partitionedStorage.CountAsync(_testEntityType, null, _ct);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task CountAsync_with_string_filter_should_count_matching_entities()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        await CreateEntityAsync(partitionedStorage, "Alice Smith", ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Bob Jones", ct: _ct);
        await CreateEntityAsync(partitionedStorage, "Charlie Smith", ct: _ct);

        var filter = new StringField("name").Contains("Smith");

        // Act
        var count = await partitionedStorage.CountAsync(_testEntityType, filter, _ct);

        // Assert
        count.ShouldBe(2);
    }
}
