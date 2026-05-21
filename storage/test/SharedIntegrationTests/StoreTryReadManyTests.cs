// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;

namespace Duende.Storage.IntegrationTests;

public partial class StoreTryReadManyTests
{
    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;
    private readonly Ct _ct = TestContext.Current.CancellationToken;


    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
        });

    [Fact]
    public async Task TryReadManyReturnsAllFoundIdsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var id3 = UuidV7.New();
        var value1 = new TestDso($"v1-{Guid.NewGuid()}");
        var value2 = new TestDso($"v2-{Guid.NewGuid()}");
        var value3 = new TestDso($"v3-{Guid.NewGuid()}");

        (await store.CreateAsync(id1, value1, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id2, value2, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id3, value3, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id1, id2, id3 }, 100, _ct);

        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => r.Found);
        results.Select(r => r.Id).ShouldBe([id1.Value, id2.Value, id3.Value], ignoreOrder: true);
    }

    [Fact]
    public async Task TryReadManySkipsMissingIdsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var missingId = UuidV7.New();
        var value1 = new TestDso($"v1-{Guid.NewGuid()}");
        var value2 = new TestDso($"v2-{Guid.NewGuid()}");

        (await store.CreateAsync(id1, value1, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id2, value2, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id1, missingId, id2 }, 100, _ct);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(r => r.Found);
        results.Select(r => r.Id).ShouldBe([id1.Value, id2.Value], ignoreOrder: true);
    }

    [Fact]
    public async Task TryReadManyReturnsEmptyListWhenAllIdsMissingAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var missingId1 = UuidV7.New();
        var missingId2 = UuidV7.New();

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { missingId1, missingId2 }, 100, _ct);

        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task TryReadManyReturnsEmptyListWhenInputIsEmptyAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7>(), 100, _ct);

        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task TryReadManyThrowsWhenIdsExceedsMaximumAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var ids = Enumerable.Range(0, 5).Select(_ => UuidV7.New()).ToHashSet();

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => store.TryReadManyAsync(EntityType, ids, 3, _ct));

        ex.Message.ShouldContain("5");
        ex.Message.ShouldContain("3");
    }

    [Fact]
    public async Task TryReadManySucceedsWhenIdsEqualsMaximumAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        (await store.CreateAsync(id1, new TestDso($"v1-{Guid.NewGuid()}"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id2, new TestDso($"v2-{Guid.NewGuid()}"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        // Exactly at the maximum — should not throw
        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id1, id2 }, 2, _ct);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TryReadManyReturnsCorrectDsoValuesAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        var value = new TestDso($"expected-{Guid.NewGuid()}");
        (await store.CreateAsync(id, value, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id }, 100, _ct);

        results.Count.ShouldBe(1);
        var result = results[0];
        result.Found.ShouldBeTrue();
        result.Id.ShouldBe(id.Value);
        result.Version.ShouldBe(1);
        var dso = result.Dso.ShouldBeOfType<TestDso>();
        dso.Value.ShouldBe(value.Value);
    }

    [Fact]
    public async Task TryReadManyReflectsUpdatedVersionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        var original = new TestDso($"original-{Guid.NewGuid()}");
        var updated = new TestDso($"updated-{Guid.NewGuid()}");
        (await store.CreateAsync(id, original, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version = (await store.TryReadAsync(EntityType, id, _ct)).Version!.Value;
        (await store.UpdateAsync(id, updated, version, [], [], expiration: null, [], _ct)).ShouldBe(UpdateResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id }, 100, _ct);

        results.Count.ShouldBe(1);
        results[0].Version.ShouldBe(version + 1);
        ((TestDso)results[0].Dso!).Value.ShouldBe(updated.Value);
    }

    [Fact]
    public async Task TryReadManyOmitsDeletedEntitiesAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        (await store.CreateAsync(id1, new TestDso("keep"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id2, new TestDso("delete"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        (await store.DeleteAsync(EntityType, id2, [], _ct)).ShouldBe(DeleteResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { id1, id2 }, 100, _ct);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(id1.Value);
    }
}
