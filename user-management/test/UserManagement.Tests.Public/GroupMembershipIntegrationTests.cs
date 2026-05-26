// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Membership;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class GroupMembershipIntegrationTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IGroupAdmin _groupAdmin = null!;
    private IMembershipAdmin _membership = null!;
    private ServiceProvider _serviceProvider = null!;
    private int _counter;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membership = _serviceProvider.GetRequiredService<IMembershipAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task<(GroupId Id, DataVersion Version)> CreateGroup(string? nameSuffix = null)
    {
        var name = GroupName.Create($"group-{Interlocked.Increment(ref _counter)}-{nameSuffix ?? Guid.NewGuid().ToString("N")}");
        var result = await _groupAdmin.CreateAsync(new Group { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return (result.Id!.Value, result.Version!.Value);
    }

    private static UserSubjectId CreateUser() => UserSubjectId.New();

    [Fact]
    public async Task AssignGroupAsync_returns_success()
    {
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();

        var result = await _membership.AssignGroupAsync(userId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignGroupAsync_is_idempotent()
    {
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();

        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        var result = await _membership.AssignGroupAsync(userId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignGroupAsync_with_non_existent_group_returns_error()
    {
        var nonExistentGroupId = GroupId.New();
        var userId = CreateUser();

        var result = await _membership.AssignGroupAsync(userId, nonExistentGroupId, _ct);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveGroupAsync_returns_success()
    {
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();
        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveGroupAsync(userId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveGroupAsync_is_idempotent()
    {
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();
        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.RemoveGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveGroupAsync(userId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GetMembersInGroupAsync_returns_members()
    {
        var (groupId, _) = await CreateGroup();
        var userId1 = CreateUser();
        var userId2 = CreateUser();
        (await _membership.AssignGroupAsync(userId1, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(userId2, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInGroupAsync(groupId, null, _ct);

        result.Items.ShouldContain(u => u.SubjectId == userId1);
        result.Items.ShouldContain(u => u.SubjectId == userId2);
    }

    [Fact]
    public async Task GetMembersInGroupAsync_excludes_non_members()
    {
        var (groupId, _) = await CreateGroup();
        var userA = CreateUser();
        var userB = CreateUser();
        (await _membership.AssignGroupAsync(userA, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInGroupAsync(groupId, null, _ct);

        result.Items.ShouldContain(u => u.SubjectId == userA);
        result.Items.ShouldNotContain(u => u.SubjectId == userB);
    }

    [Fact]
    public async Task GetMembersInGroupAsync_with_pagination_returns_paged_results()
    {
        var (groupId, _) = await CreateGroup();
        for (var i = 0; i < 5; i++)
        {
            var userId = CreateUser();
            (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        }

        var result = await _membership.GetMembersInGroupAsync(groupId, DataRange.FromPage(1, 2), _ct);

        result.Items.Count.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetMembersInGroupAsync_after_removal_excludes_removed_member()
    {
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();
        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.RemoveGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInGroupAsync(groupId, null, _ct);

        result.Items.ShouldNotContain(u => u.SubjectId == userId);
    }

    [Fact]
    public async Task GetGroupsAsync_returns_memberships()
    {
        var userId = CreateUser();
        var (groupId1, _) = await CreateGroup();
        var (groupId2, _) = await CreateGroup();
        (await _membership.AssignGroupAsync(userId, groupId1, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(userId, groupId2, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetGroupsAsync(userId, null, _ct);

        result.Items.ShouldContain(g => g.Id == groupId1);
        result.Items.ShouldContain(g => g.Id == groupId2);
    }

    [Fact]
    public async Task GetGroupsAsync_excludes_non_memberships()
    {
        var userId = CreateUser();
        var (groupA, _) = await CreateGroup();
        var (groupB, _) = await CreateGroup();
        (await _membership.AssignGroupAsync(userId, groupA, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetGroupsAsync(userId, null, _ct);

        result.Items.ShouldContain(g => g.Id == groupA);
        result.Items.ShouldNotContain(g => g.Id == groupB);
    }

    [Fact]
    public async Task GetGroupsAsync_with_pagination_returns_paged_results()
    {
        var userId = CreateUser();
        for (var i = 0; i < 5; i++)
        {
            var (groupId, _) = await CreateGroup();
            (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        }

        var result = await _membership.GetGroupsAsync(userId, DataRange.FromPage(1, 2), _ct);

        result.Items.Count.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetGroupsAsync_after_removal_excludes_removed_group()
    {
        var userId = CreateUser();
        var (groupId, _) = await CreateGroup();
        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.RemoveGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetGroupsAsync(userId, null, _ct);

        result.Items.ShouldNotContain(g => g.Id == groupId);
    }

}
