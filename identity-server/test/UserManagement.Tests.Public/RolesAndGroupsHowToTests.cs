// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Membership;
using Microsoft.Extensions.DependencyInjection;

using Duende.IdentityServer.Models;

namespace Duende.Platform.UserManagement;

public sealed class RolesAndGroupsHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IRoleAdmin _roleAdmin = null!;
    private IGroupAdmin _groupAdmin = null!;
    private IMembershipAdmin _membershipAdmin = null!;
    private IUserAdmin _userAdmin = null!;
    private ServiceProvider _serviceProvider = null!;
    private int _counter;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await MembershipServiceProviderFactory.CreateAsync();
        _roleAdmin = _serviceProvider.GetRequiredService<IRoleAdmin>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membershipAdmin = _serviceProvider.GetRequiredService<IMembershipAdmin>();
        _userAdmin = _serviceProvider.GetRequiredService<IUserAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private RoleName UniqueRoleName() => RoleName.Create($"role-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}");

    private GroupName UniqueGroupName() => GroupName.Create($"group-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}");

    private async Task<RoleId> CreateRoleAsync()
    {
        var result = await _roleAdmin.CreateAsync(new Role { Name = UniqueRoleName() }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!.Value;
    }

    private async Task<GroupId> CreateGroupAsync()
    {
        var result = await _groupAdmin.CreateAsync(new Group { Name = UniqueGroupName() }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!.Value;
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<RoleId> CreateRole(IRoleAdmin roleAdmin, RoleName name, Ct ct)
    {
        // begin-snippet: roles-create
        var created = await roleAdmin.CreateAsync(
            new Role
            {
                Name = name,
                Description = RoleDescription.Create("Can manage billing for a tenant")
            },
            ct);

        if (!created.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to create role: {created}");
        }

        var roleId = created.Id!.Value;
        var roleVersion = created.Version!.Value;
        // end-snippet

        _ = roleVersion;
        return roleId;
    }

    private static async Task<GroupId> CreateGroup(IGroupAdmin groupAdmin, GroupName name, Ct ct)
    {
        // begin-snippet: groups-create
        var created = await groupAdmin.CreateAsync(
            new Group
            {
                Name = name,
                Description = GroupDescription.Create("Everyone in the billing department")
            },
            ct);

        if (!created.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to create group: {created}");
        }

        var groupId = created.Id!.Value;
        // end-snippet

        return groupId;
    }

    private static async Task AssignMembership(IMembershipAdmin membershipAdmin, UserSubjectId subjectId, RoleId directRoleId, GroupId groupId, RoleId groupRoleId, Ct ct)
    {
        // begin-snippet: membership-assign
        // A role assigned directly to the subject.
        var directRoleResult = await membershipAdmin.AssignRoleAsync(subjectId, directRoleId, ct);

        // Group membership for the subject, and a role assigned to the group.
        // Both calls auto-create the membership and are idempotent.
        var groupResult = await membershipAdmin.AssignGroupAsync(subjectId, groupId, ct);
        var groupRoleResult = await membershipAdmin.AssignRoleToGroupAsync(groupRoleId, groupId, ct);
        // end-snippet

        directRoleResult.IsSuccess.ShouldBeTrue();
        groupResult.IsSuccess.ShouldBeTrue();
        groupRoleResult.IsSuccess.ShouldBeTrue();
    }

    private static async Task<(IReadOnlyList<RoleId> Direct, IReadOnlyList<RoleId> Transitive)> ReadRoles(IMembershipAdmin membershipAdmin, UserSubjectId subjectId, Ct ct)
    {
        // begin-snippet: membership-transitive-roles
        // Roles assigned directly to the subject.
        var directRoles = await membershipAdmin.GetDirectRolesAsync(subjectId, DataRange.FromPage(1), ct);

        // Roles the subject has through group membership. May overlap with directRoles: these are
        // separate assignment paths, not a disjoint partition.
        var transitiveRoles = await membershipAdmin.GetTransitiveRolesAsync(subjectId, DataRange.FromPage(1), ct);
        // end-snippet

        return (
            directRoles.Results.Select(r => r.Id).ToList(),
            transitiveRoles.Results.Select(r => r.Id).ToList());
    }

    private static async Task<DataVersion> UpdateRoleDescriptionWithVersion(IRoleAdmin roleAdmin, RoleId roleId, DataVersion expectedVersion, Ct ct)
    {
        // begin-snippet: roles-update-with-version
        var current = await roleAdmin.GetAsync(roleId, ct);
        if (!current.Found)
        {
            throw new InvalidOperationException($"Role {roleId} not found.");
        }

        var updated = await roleAdmin.UpdateAsync(
            roleId,
            new Role { Name = current.Item!.Name, Description = RoleDescription.Create("Updated description") },
            expectedVersion,
            ct);

        if (!updated.IsSuccess)
        {
            // expectedVersion no longer matches the stored version: someone else updated the
            // role since it was read. Re-read, reconcile, and retry with the new version.
            throw new InvalidOperationException($"Version conflict updating role: {updated}");
        }
        // end-snippet

        return updated.Version!.Value;
    }

    private static async Task<IReadOnlyList<RoleListItem>> QueryRoles(IRoleAdmin roleAdmin, string namePrefix, Ct ct)
    {
        // begin-snippet: roles-query
        var page = await roleAdmin.QueryAsync(
            QueryRequest.Create<RoleFilter, RoleSortField>(
                filter: new RoleFilter { Name = namePrefix },
                sort: SortBy.Field(RoleSortField.Name, Duende.IdentityServer.Models.SortDirection.Ascending),
                range: DataRange.FromPage(1, 20)),
            ct);

        var roles = page.Results.ToList();
        // end-snippet

        return roles;
    }

    private static async Task<bool> RemoveUser(IUserAdmin userAdmin, IMembershipAdmin membershipAdmin, UserSubjectId subjectId, RoleId roleId, GroupId groupId, Ct ct)
    {
        // begin-snippet: user-remove
        // IUserAdmin.TryRemoveAsync removes the user's profile and authenticators, but not
        // their role and group assignments. Remove known assignments first.
        _ = await membershipAdmin.RemoveRoleAsync(subjectId, roleId, ct);
        _ = await membershipAdmin.RemoveGroupAsync(subjectId, groupId, ct);

        var removed = await userAdmin.TryRemoveAsync(subjectId, ct);
        // end-snippet

        return removed;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task membership_shows_direct_role_and_transitive_only_role()
    {
        var subjectId = UserSubjectId.New();
        var directRoleId = await CreateRole(_roleAdmin, UniqueRoleName(), _ct);
        var groupRoleId = await CreateRole(_roleAdmin, UniqueRoleName(), _ct);
        var groupId = await CreateGroup(_groupAdmin, UniqueGroupName(), _ct);

        await AssignMembership(_membershipAdmin, subjectId, directRoleId, groupId, groupRoleId, _ct);
        var (direct, transitive) = await ReadRoles(_membershipAdmin, subjectId, _ct);

        direct.ShouldContain(directRoleId);
        direct.ShouldNotContain(groupRoleId);
        transitive.ShouldContain(groupRoleId);
        transitive.ShouldNotContain(directRoleId);
    }

    [Fact]
    public async Task a_role_assigned_both_directly_and_through_a_group_appears_in_both_results()
    {
        var subjectId = UserSubjectId.New();
        var sharedRoleId = await CreateRole(_roleAdmin, UniqueRoleName(), _ct);
        var groupId = await CreateGroup(_groupAdmin, UniqueGroupName(), _ct);

        (await _membershipAdmin.AssignRoleAsync(subjectId, sharedRoleId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membershipAdmin.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membershipAdmin.AssignRoleToGroupAsync(sharedRoleId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var (direct, transitive) = await ReadRoles(_membershipAdmin, subjectId, _ct);

        direct.ShouldContain(sharedRoleId);
        transitive.ShouldContain(sharedRoleId);
    }

    [Fact]
    public async Task update_with_stale_version_returns_conflict()
    {
        var roleId = await CreateRoleAsync();
        var original = await _roleAdmin.GetAsync(roleId, _ct);
        original.Found.ShouldBeTrue();
        var staleVersion = original.Version!.Value;

        // Someone else updates the role first, advancing its version.
        var firstUpdate = await _roleAdmin.UpdateAsync(
            roleId,
            new Role { Name = UniqueRoleName(), Description = RoleDescription.Create("First update") },
            staleVersion,
            _ct);
        firstUpdate.IsSuccess.ShouldBeTrue();

        // Updating again with the now-stale version throws, because UpdateRoleDescriptionWithVersion
        // treats a failed save as a version conflict.
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => UpdateRoleDescriptionWithVersion(_roleAdmin, roleId, staleVersion, _ct));
    }

    [Fact]
    public async Task update_with_current_version_succeeds()
    {
        var roleId = await CreateRoleAsync();
        var original = await _roleAdmin.GetAsync(roleId, _ct);
        original.Found.ShouldBeTrue();

        var newVersion = await UpdateRoleDescriptionWithVersion(_roleAdmin, roleId, original.Version!.Value, _ct);

        newVersion.ShouldNotBe(original.Version!.Value);
    }

    [Fact]
    public async Task query_with_name_filter_returns_matching_roles()
    {
        var uniquePrefix = $"query-{Guid.NewGuid():N}";
        var name = RoleName.Create($"{uniquePrefix}-role");
        _ = await CreateRole(_roleAdmin, name, _ct);

        var results = await QueryRoles(_roleAdmin, uniquePrefix, _ct);

        results.ShouldContain(r => r.Name == name);
    }

    [Fact]
    public async Task remove_user_deletes_the_user_but_membership_must_be_removed_explicitly()
    {
        var subjectId = UserSubjectId.New();
        var roleId = await CreateRoleAsync();
        var groupId = await CreateGroupAsync();
        (await _membershipAdmin.AssignRoleAsync(subjectId, roleId, _ct)).IsSuccess.ShouldBeTrue();
        (await _membershipAdmin.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();

        var removed = await RemoveUser(_userAdmin, _membershipAdmin, subjectId, roleId, groupId, _ct);

        removed.ShouldBeTrue();
        (await _membershipAdmin.GetMembersInRoleAsync(roleId, DataRange.FromPage(1), _ct))
            .Results.ShouldNotContain(m => m.SubjectId == subjectId);
        (await _membershipAdmin.GetMembersInGroupAsync(groupId, DataRange.FromPage(1), _ct))
            .Results.ShouldNotContain(m => m.SubjectId == subjectId);
    }
}
