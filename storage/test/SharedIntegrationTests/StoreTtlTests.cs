// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

public partial class StoreTtlTests
{


    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateWithTtlReadBeforeExpiryShouldSucceedAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"ttl-{Guid.NewGuid()}");

        var result = await storage.CreateAsync(id, value, [], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct);

        result.ShouldBe(CreateResult.Success);
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateWithTtlReadAfterExpiryShouldStillBeFoundAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"ttl-{Guid.NewGuid()}");

        (await storage.CreateAsync(id, value, [], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct))
            .ShouldBe(CreateResult.Success);

        // Advance past expiration — storage still returns the record (TTL is best-effort)
        tp.Advance(TimeSpan.FromHours(2));

        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateWithPastTtlShouldBeNoopAndReturnSuccessAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"past-{Guid.NewGuid()}");
        var pastExpiration = Expiration.AtAbsolute(tp.GetUtcNow().AddHours(-1));

        var result = await storage.CreateAsync(id, value, [], [], pastExpiration, [], _ct);

        result.ShouldBe(CreateResult.Success);
        // Entity should NOT have been stored
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateWithTtlReadAfterExpiryShouldStillBeFoundAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"val-{Guid.NewGuid()}");

        (await storage.CreateAsync(id, value, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version = (await storage.TryReadAsync(EntityType, id, _ct)).Version.ShouldNotBeNull();

        var updated = new TestDso($"updated-{Guid.NewGuid()}");
        (await storage.UpdateAsync(id, updated, version, [], [],
            Expiration.InRelative(TimeSpan.FromMinutes(30)), [], _ct)).ShouldBe(UpdateResult.Success);

        // Before expiry — visible
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();

        // Advance past expiration — still visible (TTL is best-effort)
        tp.Advance(TimeSpan.FromHours(1));

        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateWithPastTtlEntityShouldStillBeFoundAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"val-{Guid.NewGuid()}");

        (await storage.CreateAsync(id, value, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version = (await storage.TryReadAsync(EntityType, id, _ct)).Version.ShouldNotBeNull();

        var pastExpiration = Expiration.AtAbsolute(tp.GetUtcNow().AddHours(-1));
        var result = await storage.UpdateAsync(id, value, version, [], [], pastExpiration, [], _ct);

        result.ShouldBe(UpdateResult.Success);
        // Entity is still returned by reads (TTL is best-effort, domain decides visibility)
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateWithNullExpirationShouldNotChangeExistingAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"val-{Guid.NewGuid()}");

        // Create with 1-hour TTL
        (await storage.CreateAsync(id, value, [], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct))
            .ShouldBe(CreateResult.Success);
        var version = (await storage.TryReadAsync(EntityType, id, _ct)).Version.ShouldNotBeNull();

        // Update with null expiration (don't change)
        var updated = new TestDso($"updated-{Guid.NewGuid()}");
        (await storage.UpdateAsync(id, updated, version, [], [], expiration: null, [], _ct))
            .ShouldBe(UpdateResult.Success);

        // Advance past original expiration — entity still returned (TTL is best-effort)
        tp.Advance(TimeSpan.FromHours(2));
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateWithNoExpirationShouldClearExistingExpirationAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"val-{Guid.NewGuid()}");

        // Create with 1-hour TTL
        (await storage.CreateAsync(id, value, [], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct))
            .ShouldBe(CreateResult.Success);
        var version = (await storage.TryReadAsync(EntityType, id, _ct)).Version.ShouldNotBeNull();

        // Update with NoExpiration (explicitly clear)
        var updated = new TestDso($"updated-{Guid.NewGuid()}");
        (await storage.UpdateAsync(id, updated, version, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(UpdateResult.Success);

        // Advance past original expiration — entity should still be visible (expiration was cleared)
        tp.Advance(TimeSpan.FromHours(2));
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task TryReadByKeyShouldReturnExpiredRecordsAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"val-{Guid.NewGuid()}");
        var key = new TestJsonKeyDsk($"key-{Guid.NewGuid()}");

        (await storage.CreateAsync(id, value, [DataStorageKey.Create(key)], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct))
            .ShouldBe(CreateResult.Success);

        // Before expiry
        (await storage.TryReadAsync(EntityType, DataStorageKey.Create(key), _ct)).Found.ShouldBeTrue();

        tp.Advance(TimeSpan.FromHours(2));

        // After expiry — still returned (TTL is best-effort)
        (await storage.TryReadAsync(EntityType, DataStorageKey.Create(key), _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task TryReadManyShouldReturnExpiredRecordsAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var value1 = new TestDso($"val1-{Guid.NewGuid()}");
        var value2 = new TestDso($"val2-{Guid.NewGuid()}");

        // id1 expires in 1 hour, id2 never expires
        (await storage.CreateAsync(id1, value1, [], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct))
            .ShouldBe(CreateResult.Success);
        (await storage.CreateAsync(id2, value2, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        // Before expiry — both visible
        var before = await storage.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id1, id2 }, 100, _ct);
        before.Count.ShouldBe(2);

        tp.Advance(TimeSpan.FromHours(2));

        // After expiry — both still returned (TTL is best-effort)
        var after = await storage.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id1, id2 }, 100, _ct);
        after.Count.ShouldBe(2);
    }

    [Fact]
    public async Task QueryShouldReturnExpiredEntitiesAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var queryStorage = fixture.Storage;
        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var value1 = new TestDso($"expires-{Guid.NewGuid()}");
        var value2 = new TestDso($"persists-{Guid.NewGuid()}");

        // id1 expires, id2 does not
        (await storage.CreateAsync(id1, value1, [], [], Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct))
            .ShouldBe(CreateResult.Success);
        (await storage.CreateAsync(id2, value2, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        // Before expiry
        var beforeResult = await queryStorage.QueryAsync<TestDso>(
            EntityType, Query.All(), SortParameter.Empty, DataRange.FromPage(1, 100), _ct);
        beforeResult.Items.Count.ShouldBeGreaterThanOrEqualTo(2);

        tp.Advance(TimeSpan.FromHours(2));

        // After expiry — both still returned (TTL is best-effort, domain decides)
        var afterResult = await queryStorage.QueryAsync<TestDso>(
            EntityType, Query.All(), SortParameter.Empty, DataRange.FromPage(1, 100), _ct);
        afterResult.Items.ShouldContain(item => item.Value.Value == value1.Value);
        afterResult.Items.ShouldContain(item => item.Value.Value == value2.Value);
    }

    [Fact]
    public async Task BatchUpdateWithTtlShouldStillBeFoundAfterExpiryAsync()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var storage = fixture.Storage;
        var id = UuidV7.New();
        var value = new TestDso($"batch-{Guid.NewGuid()}");

        (await storage.CreateAsync(id, value, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version = (await storage.TryReadAsync(EntityType, id, _ct)).Version.ShouldNotBeNull();

        var updatedValue = new TestDso($"batch-updated-{Guid.NewGuid()}");
        var operations = new IStorageOperation[]
        {
            UpdateOperation.For(id, updatedValue, version, [], SearchFieldCollection.Empty,
                Expiration.InRelative(TimeSpan.FromHours(1)))
        };

        var result = await storage.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();

        tp.Advance(TimeSpan.FromHours(2));

        // After expiry — still found (TTL is best-effort)
        (await storage.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
    }


    private async Task<IStorageFixture> CreateProviderAsync(FakeTimeProvider tp) =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton(tp);
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
}
