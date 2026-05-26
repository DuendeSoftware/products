// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Membership;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class MembershipAdminIntegrationTests : IAsyncLifetime, IAsyncDisposable
{
    private IMembershipAdmin _membership = null!;
    private IRoleAdmin _roleAdmin = null!;
    private IGroupAdmin _groupAdmin = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ServiceProvider _serviceProvider = null!;
    private int _counter;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await MembershipServiceProviderFactory.CreateAsync();
        _membership = _serviceProvider.GetRequiredService<IMembershipAdmin>();
        _roleAdmin = _serviceProvider.GetRequiredService<IRoleAdmin>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task<RoleId> CreateRoleAsync()
    {
        var name = RoleName.Create($"role-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}");
        var result = await _roleAdmin.CreateAsync(new Role { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!.Value;
    }

    private async Task<GroupId> CreateGroupAsync()
    {
        var name = GroupName.Create($"group-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}");
        var result = await _groupAdmin.CreateAsync(new Group { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!.Value;
    }

    private static UserSubjectId NewSubjectId() => UserSubjectId.New();

    [Fact]
    public async Task assign_role_returns_success()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();

        var result = await _membership.AssignRoleAsync(subjectId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task assign_role_is_idempotent()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();

        (await _membership.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        var result = await _membership.AssignRoleAsync(subjectId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task assign_role_with_nonexistent_role_returns_error()
    {
        var subjectId = NewSubjectId();
        var nonExistentRoleId = RoleId.New();

        var result = await _membership.AssignRoleAsync(subjectId, nonExistentRoleId, _ct);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task remove_role_returns_success()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();
        (await _membership.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveRoleAsync(subjectId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task remove_role_is_idempotent()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();
        (await _membership.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.RemoveRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveRoleAsync(subjectId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task remove_role_succeeds_when_no_membership_exists()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();

        var result = await _membership.RemoveRoleAsync(subjectId, roleId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task assign_group_returns_success()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();

        var result = await _membership.AssignGroupAsync(subjectId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task assign_group_is_idempotent()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();

        (await _membership.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        var result = await _membership.AssignGroupAsync(subjectId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task assign_group_with_nonexistent_group_returns_error()
    {
        var subjectId = NewSubjectId();
        var nonExistentGroupId = GroupId.New();

        var result = await _membership.AssignGroupAsync(subjectId, nonExistentGroupId, _ct);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task remove_group_returns_success()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();
        (await _membership.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.RemoveGroupAsync(subjectId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task remove_group_succeeds_when_no_membership_exists()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();

        var result = await _membership.RemoveGroupAsync(subjectId, groupId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task get_direct_roles_returns_assigned_roles()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();
        (await _membership.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetDirectRolesAsync(subjectId, null, _ct);

        result.Items.ShouldContain(r => r.Id == roleId);
    }

    [Fact]
    public async Task get_direct_roles_returns_empty_when_no_membership()
    {
        var subjectId = NewSubjectId();

        var result = await _membership.GetDirectRolesAsync(subjectId, null, _ct);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task get_direct_roles_does_not_include_transitive_roles()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();
        var roleId = await CreateRoleAsync();
        (await _membership.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignRoleToGroupAsync(roleId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetDirectRolesAsync(subjectId, null, _ct);

        result.Items.ShouldNotContain(r => r.Id == roleId);
    }
    [Fact]
    public async Task get_transitive_roles_returns_empty_when_no_membership()
    {
        var subjectId = NewSubjectId();

        var result = await _membership.GetTransitiveRolesAsync(subjectId, null, _ct);

        result.Items.ShouldBeEmpty();
    }
    [Fact]
    public async Task get_groups_returns_assigned_groups()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();
        (await _membership.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetGroupsAsync(subjectId, null, _ct);

        result.Items.ShouldContain(g => g.Id == groupId);
    }

    [Fact]
    public async Task get_groups_returns_empty_when_no_membership()
    {
        var subjectId = NewSubjectId();

        var result = await _membership.GetGroupsAsync(subjectId, null, _ct);

        result.Items.ShouldBeEmpty();
    }
    [Fact]
    public async Task get_members_in_role_returns_assigned_subjects()
    {
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();
        (await _membership.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInRoleAsync(roleId, null, _ct);

        result.Items.ShouldContain(m => m.SubjectId == subjectId);
    }

    [Fact]
    public async Task get_members_in_group_returns_assigned_subjects()
    {
        var subjectId = NewSubjectId();
        var groupId = await CreateGroupAsync();
        (await _membership.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _membership.GetMembersInGroupAsync(groupId, null, _ct);

        result.Items.ShouldContain(m => m.SubjectId == subjectId);
    }
    [Fact]
    public async Task operations_work_without_any_user_profile_existing()
    {
        // No profile is created — only a raw subject ID
        var subjectId = NewSubjectId();
        var roleId = await CreateRoleAsync();
        var groupId = await CreateGroupAsync();

        // All operations should succeed without a profile
        (await _membership.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membership.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var roles = await _membership.GetDirectRolesAsync(subjectId, null, _ct);
        var groups = await _membership.GetGroupsAsync(subjectId, null, _ct);
        var membersInRole = await _membership.GetMembersInRoleAsync(roleId, null, _ct);
        var membersInGroup = await _membership.GetMembersInGroupAsync(groupId, null, _ct);

        roles.Items.ShouldContain(r => r.Id == roleId);
        groups.Items.ShouldContain(g => g.Id == groupId);
        membersInRole.Items.ShouldContain(m => m.SubjectId == subjectId);
        membersInGroup.Items.ShouldContain(m => m.SubjectId == subjectId);
    }
}
