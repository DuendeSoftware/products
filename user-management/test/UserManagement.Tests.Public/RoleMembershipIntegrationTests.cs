// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Membership;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class RoleMembershipIntegrationTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IGroupAdmin _groupAdmin = null!;
    private IRoleAdmin _roleAdmin = null!;
    private IMembershipAdmin _membership = null!;
    private ServiceProvider _serviceProvider = null!;
    private int _counter;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _roleAdmin = _serviceProvider.GetRequiredService<IRoleAdmin>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membership = _serviceProvider.GetRequiredService<IMembershipAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task<(RoleId Id, DataVersion Version)> CreateRole(string? nameSuffix = null)
    {
        var name = RoleName.Create($"role-{Interlocked.Increment(ref _counter)}-{nameSuffix ?? Guid.NewGuid().ToString("N")}");
        var result = await _roleAdmin.CreateAsync(new Role { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return (result.Id!.Value, result.Version!.Value);
    }

    private async Task<(GroupId Id, DataVersion Version)> CreateGroup(string? nameSuffix = null)
    {
        var name = GroupName.Create($"group-{Interlocked.Increment(ref _counter)}-{nameSuffix ?? Guid.NewGuid().ToString("N")}");
        var result = await _groupAdmin.CreateAsync(new Group { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return (result.Id!.Value, result.Version!.Value);
    }

    private static UserSubjectId CreateUser() => UserSubjectId.New();


    [Fact]
    public async Task AssignRoleAsync_returns_success()
    {
        var (roleId, _) = await CreateRole();
        var userId = CreateUser();

        var result = await _membership.AssignRoleAsync(userId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignRoleAsync_is_idempotent()
    {
        var (roleId, _) = await CreateRole();
        var userId = CreateUser();

        (await _membership.AssignRoleAsync(userId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        var result = await _membership.AssignRoleAsync(userId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignRoleAsync_with_non_existent_role_returns_error()
    {
        var nonExistentRoleId = RoleId.New();
        var userId = CreateUser();

        var result = await _membership.AssignRoleAsync(userId, nonExistentRoleId, _ct);

        result.IsSuccess.ShouldBeFalse();
    }



    [Fact]
    public async Task RemoveRoleAsync_returns_success()
    {
        var (roleId, _) = await CreateRole();
        var userId = CreateUser();
        (await _membership.AssignRoleAsync(userId, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveRoleAsync(userId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveRoleAsync_is_idempotent()
    {
        var (roleId, _) = await CreateRole();
        var userId = CreateUser();
        (await _membership.AssignRoleAsync(userId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.RemoveRoleAsync(userId, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveRoleAsync(userId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }



    [Fact]
    public async Task AssignRoleToGroupAsync_returns_success()
    {
        var (roleId, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();

        var result = await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignRoleToGroupAsync_is_idempotent()
    {
        var (roleId, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();

        (await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        var result = await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignRoleToGroupAsync_with_non_existent_role_returns_error()
    {
        var nonExistentRoleId = RoleId.New();
        var (groupId, _) = await CreateGroup();

        var result = await _membership.AssignRoleToGroupAsync(nonExistentRoleId, groupId, _ct);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task AssignRoleToGroupAsync_with_non_existent_group_returns_error()
    {
        var (roleId, _) = await CreateRole();
        var nonExistentGroupId = GroupId.New();

        var result = await _membership.AssignRoleToGroupAsync(roleId, nonExistentGroupId, _ct);

        result.IsSuccess.ShouldBeFalse();
    }



    [Fact]
    public async Task RemoveRoleFromGroupAsync_returns_success()
    {
        var (roleId, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();
        (await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveRoleFromGroupAsync(roleId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveRoleFromGroupAsync_is_idempotent()
    {
        var (roleId, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();
        (await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.RemoveRoleFromGroupAsync(roleId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveRoleFromGroupAsync(roleId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }



    [Fact]
    public async Task GetMembersInRoleAsync_returns_assigned_members()
    {
        var (roleId, _) = await CreateRole();
        var userId1 = CreateUser();
        var userId2 = CreateUser();
        (await _membership.AssignRoleAsync(userId1, roleId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignRoleAsync(userId2, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInRoleAsync(roleId, null, _ct);

        result.Items.ShouldContain(u => u.SubjectId == userId1);
        result.Items.ShouldContain(u => u.SubjectId == userId2);
    }

    [Fact]
    public async Task GetMembersInRoleAsync_excludes_unassigned_members()
    {
        var (roleId, _) = await CreateRole();
        var userA = CreateUser();
        var userB = CreateUser();
        (await _membership.AssignRoleAsync(userA, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInRoleAsync(roleId, null, _ct);

        result.Items.ShouldContain(u => u.SubjectId == userA);
        result.Items.ShouldNotContain(u => u.SubjectId == userB);
    }

    [Fact]
    public async Task GetMembersInRoleAsync_with_pagination_returns_paged_results()
    {
        var (roleId, _) = await CreateRole();
        for (var i = 0; i < 5; i++)
        {
            var userId = CreateUser();
            (await _membership.AssignRoleAsync(userId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        }

        var result = await _membership.GetMembersInRoleAsync(roleId, DataRange.FromPage(1, 2), _ct);

        result.Items.Count.ShouldBeLessThanOrEqualTo(2);
    }



    [Fact]
    public async Task GetGroupsInRoleAsync_returns_assigned_groups()
    {
        var (roleId, _) = await CreateRole();
        var (groupId1, _) = await CreateGroup();
        var (groupId2, _) = await CreateGroup();
        (await _membership.AssignRoleToGroupAsync(roleId, groupId1, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignRoleToGroupAsync(roleId, groupId2, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetGroupsInRoleAsync(roleId, null, _ct);

        result.Items.ShouldContain(g => g.Id == groupId1);
        result.Items.ShouldContain(g => g.Id == groupId2);
    }

    [Fact]
    public async Task GetGroupsInRoleAsync_excludes_unassigned_groups()
    {
        var (roleId, _) = await CreateRole();
        var (groupA, _) = await CreateGroup();
        var (groupB, _) = await CreateGroup();
        (await _membership.AssignRoleToGroupAsync(roleId, groupA, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetGroupsInRoleAsync(roleId, null, _ct);

        result.Items.ShouldContain(g => g.Id == groupA);
        result.Items.ShouldNotContain(g => g.Id == groupB);
    }



    [Fact]
    public async Task GetDirectRolesAsync_returns_assigned_roles()
    {
        var (roleId1, _) = await CreateRole();
        var (roleId2, _) = await CreateRole();
        var userId = CreateUser();
        (await _membership.AssignRoleAsync(userId, roleId1, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignRoleAsync(userId, roleId2, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetDirectRolesAsync(userId, null, _ct);

        result.Items.ShouldContain(r => r.Id == roleId1);
        result.Items.ShouldContain(r => r.Id == roleId2);
    }

    [Fact]
    public async Task GetDirectRolesAsync_excludes_transitive_roles()
    {
        // User → Group → Role (transitive)
        var (transitiveRoleId, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();

        (await _membership.AssignRoleToGroupAsync(transitiveRoleId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetDirectRolesAsync(userId, null, _ct);

        // Transitive role via group should NOT appear in direct roles
        result.Items.ShouldNotContain(r => r.Id == transitiveRoleId);
    }



    [Fact]
    public async Task GetTransitiveRolesAsync_returns_roles_via_group()
    {
        var (roleId, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();
        var userId = CreateUser();

        (await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(userId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetTransitiveRolesAsync(userId, null, _ct);

        result.Items.ShouldContain(r => r.Id == roleId);
    }

    [Fact]
    public async Task GetTransitiveRolesAsync_excludes_direct_roles()
    {
        // User has a direct role (not via group)
        var (directRoleId, _) = await CreateRole();
        var userId = CreateUser();
        (await _membership.AssignRoleAsync(userId, directRoleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetTransitiveRolesAsync(userId, null, _ct);

        // Direct role should NOT appear in transitive roles
        result.Items.ShouldNotContain(r => r.Id == directRoleId);
    }

    [Fact]
    public async Task GetTransitiveRolesAsync_with_multiple_groups_returns_union()
    {
        var (roleX, _) = await CreateRole();
        var (roleY, _) = await CreateRole();
        var (groupA, _) = await CreateGroup();
        var (groupB, _) = await CreateGroup();
        var userId = CreateUser();

        (await _membership.AssignRoleToGroupAsync(roleX, groupA, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignRoleToGroupAsync(roleY, groupB, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(userId, groupA, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(userId, groupB, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetTransitiveRolesAsync(userId, null, _ct);

        result.Items.ShouldContain(r => r.Id == roleX);
        result.Items.ShouldContain(r => r.Id == roleY);
    }



    [Fact]
    public async Task GetRolesForGroupAsync_returns_assigned_roles()
    {
        var (roleId1, _) = await CreateRole();
        var (roleId2, _) = await CreateRole();
        var (groupId, _) = await CreateGroup();
        (await _membership.AssignRoleToGroupAsync(roleId1, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignRoleToGroupAsync(roleId2, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetRolesForGroupAsync(groupId, null, _ct);

        result.Items.ShouldContain(r => r.Id == roleId1);
        result.Items.ShouldContain(r => r.Id == roleId2);
    }

}
