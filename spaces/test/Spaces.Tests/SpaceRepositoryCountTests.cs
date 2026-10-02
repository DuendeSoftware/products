// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

/// <summary>
/// Focused tests for <see cref="SpaceRepository.GetActiveCountAsync"/>. Exercises the
/// admin surface to mutate state, then reads the count through the repository so the
/// tests verify the database-level projection driven by the <c>isDeleted</c> search field.
/// </summary>
public sealed class SpaceRepositoryCountTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private SpaceRepository _repository = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = sc.BuildServiceProvider();

        await _services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _repository = _services.GetRequiredService<SpaceRepository>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task active_count_is_zero_when_no_spaces_exist()
    {
        var count = await _repository.GetActiveCountAsync(_ct);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task active_count_increases_with_each_created_space()
    {
        await CreateSpaceAsync("one", "https://one.example.com");
        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(1);

        await CreateSpaceAsync("two", "https://two.example.com");
        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(2);

        await CreateSpaceAsync("three", "https://three.example.com");
        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(3);
    }

    [Fact]
    public async Task active_count_decreases_when_space_is_deleted()
    {
        var first = await CreateSpaceAsync("one", "https://one.example.com");
        _ = await CreateSpaceAsync("two", "https://two.example.com");

        var deleted = await _admin.DeleteAsync(first, _ct);
        deleted.IsSuccess.ShouldBeTrue();

        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(1);
    }

    [Fact]
    public async Task active_count_restored_when_space_is_undeleted()
    {
        var space = await CreateSpaceAsync("one", "https://one.example.com");
        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(1);

        (await _admin.DeleteAsync(space, _ct)).IsSuccess.ShouldBeTrue();
        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(0);

        (await _admin.UndeleteAsync(space, _ct)).IsSuccess.ShouldBeTrue();
        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(1);
    }

    [Fact]
    public async Task active_count_unchanged_by_update()
    {
        var space = await CreateSpaceAsync("one", "https://one.example.com");
        var before = await _repository.GetActiveCountAsync(_ct);

        var current = await _admin.GetAsync(space, _ct);
        current.Found.ShouldBeTrue();
        current.Item!.Name = "renamed";
        (await _admin.UpdateAsync(space, current.Item, current.Version!, _ct)).IsSuccess.ShouldBeTrue();

        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(before);
    }

    [Fact]
    public async Task active_count_unchanged_by_purge_of_already_deleted_space()
    {
        var doomed = await CreateSpaceAsync("one", "https://one.example.com");
        _ = await CreateSpaceAsync("two", "https://two.example.com");

        (await _admin.DeleteAsync(doomed, _ct)).IsSuccess.ShouldBeTrue();
        var afterDelete = await _repository.GetActiveCountAsync(_ct);

        (await _admin.PurgeAsync(doomed, _ct)).IsSuccess.ShouldBeTrue();

        (await _repository.GetActiveCountAsync(_ct)).ShouldBe(afterDelete);
    }

    [Fact]
    public async Task default_space_is_not_counted()
    {
        // The default space is not persisted as a SpaceDso; the count reflects only
        // customer-created spaces, so an empty store reports zero even though runtime
        // routing always resolves a default space.
        var count = await _repository.GetActiveCountAsync(_ct);

        count.ShouldBe(0);
    }

    private async Task<SpaceId> CreateSpaceAsync(string name, string origin)
    {
        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            _ct);

        result.IsSuccess.ShouldBeTrue($"failed to create space '{name}': {string.Join("; ", (result.Errors ?? []).Select(e => e.Message))}");
        return result.Id!;
    }
}
