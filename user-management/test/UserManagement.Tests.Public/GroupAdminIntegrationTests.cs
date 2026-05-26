// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Membership;
using Microsoft.Extensions.DependencyInjection;
using SortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.Platform.UserManagement;

public sealed class GroupAdminIntegrationTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IGroupAdmin _admin = null!;
    private ServiceProvider _serviceProvider = null!;
    private int _counter;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _admin = _serviceProvider.GetRequiredService<IGroupAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private GroupName UniqueName() => GroupName.Create($"group-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}");

    private static GroupId NonExistentId() => GroupId.New();

    [Fact]
    public async Task CreateAsync_with_valid_group_returns_success()
    {
        var dto = new Group { Name = UniqueName() };

        var result = await _admin.CreateAsync(dto, _ct);

        result.IsSuccess.ShouldBeTrue();
        _ = result.Id.ShouldNotBeNull();
        _ = result.Version.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_with_name_and_description_returns_success()
    {
        var dto = new Group
        {
            Name = UniqueName(),
            Description = GroupDescription.Create("A test group description")
        };

        var result = await _admin.CreateAsync(dto, _ct);

        result.IsSuccess.ShouldBeTrue();
        _ = result.Id.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_with_duplicate_name_returns_error()
    {
        var name = UniqueName();
        var dto1 = new Group { Name = name };
        var dto2 = new Group { Name = name };

        (await _admin.CreateAsync(dto1, _ct)).IsSuccess.ShouldBeTrue();
        var result = await _admin.CreateAsync(dto2, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetAsync_with_existing_group_returns_group()
    {
        var name = UniqueName();
        var dto = new Group { Name = name };
        var createResult = await _admin.CreateAsync(dto, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(createResult.Id!.Value, _ct);

        getResult.Found.ShouldBeTrue();
        _ = getResult.Item.ShouldNotBeNull();
        getResult.Item.Name.ShouldBe(name);
        _ = getResult.Version.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsync_with_non_existent_id_returns_not_found()
    {
        var result = await _admin.GetAsync(NonExistentId(), _ct);

        result.Found.ShouldBeFalse();
        result.Item.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_returns_description_when_set()
    {
        var description = GroupDescription.Create("round-trip description");
        var dto = new Group { Name = UniqueName(), Description = description };
        var createResult = await _admin.CreateAsync(dto, _ct);

        var getResult = await _admin.GetAsync(createResult.Id!.Value, _ct);

        getResult.Found.ShouldBeTrue();
        getResult.Item!.Description.ShouldBe(description);
    }

    [Fact]
    public async Task UpdateAsync_with_valid_changes_returns_success()
    {
        var dto = new Group { Name = UniqueName(), Description = GroupDescription.Create("Original") };
        var createResult = await _admin.CreateAsync(dto, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var updatedDto = dto with { Description = GroupDescription.Create("Updated") };
        var updateResult = await _admin.UpdateAsync(createResult.Id!.Value, updatedDto, createResult.Version!.Value, _ct);

        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Version.ShouldNotBe(createResult.Version);
    }

    [Fact]
    public async Task UpdateAsync_can_verify_change_persisted()
    {
        var dto = new Group { Name = UniqueName() };
        var createResult = await _admin.CreateAsync(dto, _ct);

        var newName = UniqueName();
        var updatedDto = dto with { Name = newName };
        _ = await _admin.UpdateAsync(createResult.Id!.Value, updatedDto, createResult.Version!.Value, _ct);

        var getResult = await _admin.GetAsync(createResult.Id!.Value, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.Name.ShouldBe(newName);
    }

    [Fact]
    public async Task UpdateAsync_with_wrong_version_returns_version_conflict()
    {
        var dto = new Group { Name = UniqueName() };
        var createResult = await _admin.CreateAsync(dto, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        DataVersion wrongVersion = 999;
        var result = await _admin.UpdateAsync(createResult.Id!.Value, dto, wrongVersion, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_with_non_existent_id_returns_error()
    {
        var dto = new Group { Name = UniqueName() };

        DataVersion version = 1;
        var result = await _admin.UpdateAsync(NonExistentId(), dto, version, _ct);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_with_duplicate_name_returns_error()
    {
        var nameA = UniqueName();
        var nameB = UniqueName();
        var createA = await _admin.CreateAsync(new Group { Name = nameA }, _ct);
        var createB = await _admin.CreateAsync(new Group { Name = nameB }, _ct);
        createA.IsSuccess.ShouldBeTrue();
        createB.IsSuccess.ShouldBeTrue();

        var updatedDto = new Group { Name = nameA };
        var result = await _admin.UpdateAsync(createB.Id!.Value, updatedDto, createB.Version!.Value, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_with_existing_group_returns_success()
    {
        var createResult = await _admin.CreateAsync(new Group { Name = UniqueName() }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await _admin.DeleteAsync(createResult.Id!.Value, _ct);

        deleteResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_resource_no_longer_accessible()
    {
        var createResult = await _admin.CreateAsync(new Group { Name = UniqueName() }, _ct);
        _ = await _admin.DeleteAsync(createResult.Id!.Value, _ct);

        var getResult = await _admin.GetAsync(createResult.Id!.Value, _ct);

        getResult.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_with_non_existent_id_returns_error()
    {
        var result = await _admin.DeleteAsync(NonExistentId(), _ct);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAsync_with_no_filter_returns_all_groups()
    {
        _ = await _admin.CreateAsync(new Group { Name = UniqueName() }, _ct);
        _ = await _admin.CreateAsync(new Group { Name = UniqueName() }, _ct);
        _ = await _admin.CreateAsync(new Group { Name = UniqueName() }, _ct);

        var result = await _admin.QueryAsync(QueryRequest.Create<GroupFilter, GroupSortField>(), _ct);

        result.Items.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task QueryAsync_with_name_filter_returns_matching_groups()
    {
        var uniquePrefix = $"filter-{Guid.NewGuid():N}";
        var name = GroupName.Create($"{uniquePrefix}-group");
        _ = await _admin.CreateAsync(new Group { Name = name }, _ct);

        var result = await _admin.QueryAsync(QueryRequest.Create<GroupFilter, GroupSortField>(new GroupFilter { Name = uniquePrefix }), _ct);

        result.Items.ShouldContain(r => r.Name == name);
    }

    [Fact]
    public async Task QueryAsync_with_description_filter_returns_matching_groups()
    {
        var uniqueDesc = $"desc-{Guid.NewGuid():N}";
        var dto = new Group
        {
            Name = UniqueName(),
            Description = GroupDescription.Create(uniqueDesc)
        };
        _ = await _admin.CreateAsync(dto, _ct);

        var result = await _admin.QueryAsync(QueryRequest.Create<GroupFilter, GroupSortField>(new GroupFilter { Description = uniqueDesc }), _ct);

        result.Items.ShouldContain(r => r.Description != null && r.Description.ToString() == uniqueDesc);
    }

    [Fact]
    public async Task QueryAsync_with_pagination_returns_paged_results()
    {
        for (var i = 0; i < 5; i++)
        {
            _ = await _admin.CreateAsync(new Group { Name = UniqueName() }, _ct);
        }

        var result = await _admin.QueryAsync(QueryRequest.Create<GroupFilter, GroupSortField>(DataRange.FromPage(1, 2)), _ct);

        result.Items.Count.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task QueryAsync_with_sort_ascending_returns_sorted_results()
    {
        var name1 = GroupName.Create($"aaa-{Guid.NewGuid():N}");
        var name2 = GroupName.Create($"zzz-{Guid.NewGuid():N}");
        _ = await _admin.CreateAsync(new Group { Name = name2 }, _ct);
        _ = await _admin.CreateAsync(new Group { Name = name1 }, _ct);

        var result = await _admin.QueryAsync(
            QueryRequest.Create<GroupFilter, GroupSortField>(SortBy.Field(GroupSortField.Name, SortDirection.Ascending)),
            _ct);

        var names = result.Items.Select(r => r.Name.ToString()).ToList();
        var sortedNames = names.OrderBy(n => n).ToList();
        names.ShouldBe(sortedNames);
    }

    [Fact]
    public async Task QueryAsync_with_sort_descending_returns_sorted_results()
    {
        var name1 = GroupName.Create($"aaa-{Guid.NewGuid():N}");
        var name2 = GroupName.Create($"zzz-{Guid.NewGuid():N}");
        _ = await _admin.CreateAsync(new Group { Name = name1 }, _ct);
        _ = await _admin.CreateAsync(new Group { Name = name2 }, _ct);

        var result = await _admin.QueryAsync(
            QueryRequest.Create<GroupFilter, GroupSortField>(SortBy.Field(GroupSortField.Name, SortDirection.Descending)),
            _ct);

        var names = result.Items.Select(r => r.Name.ToString()).ToList();
        var sortedNames = names.OrderByDescending(n => n).ToList();
        names.ShouldBe(sortedNames);
    }

    [Fact]
    public async Task QueryAsync_with_name_and_description_filter_returns_intersection()
    {
        var uniquePrefix = $"intersect-{Guid.NewGuid():N}";
        var uniqueDesc = $"desc-{Guid.NewGuid():N}";

        // Matches both name and description
        var matchingDto = new Group
        {
            Name = GroupName.Create($"{uniquePrefix}-match"),
            Description = GroupDescription.Create(uniqueDesc)
        };
        _ = await _admin.CreateAsync(matchingDto, _ct);

        // Matches name but not description
        _ = await _admin.CreateAsync(new Group { Name = GroupName.Create($"{uniquePrefix}-no-desc") }, _ct);

        // Matches description but not name
        _ = await _admin.CreateAsync(new Group
        {
            Name = UniqueName(),
            Description = GroupDescription.Create(uniqueDesc)
        }, _ct);

        var result = await _admin.QueryAsync(
            QueryRequest.Create<GroupFilter, GroupSortField>(new GroupFilter { Name = uniquePrefix, Description = uniqueDesc }),
            _ct);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Name.ShouldBe(matchingDto.Name);
    }

}
