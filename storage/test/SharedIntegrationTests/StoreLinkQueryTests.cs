// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Pagination;

namespace Duende.Storage.IntegrationTests;

// ---------------------------------------------------------------------------
// Domain-flavored test DSOs: User, Role, Group
// Each gets a unique EntityType id in the test-reserved range.
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for QueryLinks across all store types.
/// Uses a Users / Roles / Groups domain to make link traversals readable:
///   UserRole  : User  → Role   (a user has a role)
///   UserGroup : User  → Group  (a user belongs to a group)
///   GroupRole : Group → Role   (a group has a role)
/// </summary>
public partial class StoreLinkQueryTests
{

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    // Entity types
    private static readonly EntityType User = UserDso.DsoVersion.EntityType;
    private static readonly EntityType Role = RoleDso.DsoVersion.EntityType;
    private static readonly EntityType Group = GroupDso.DsoVersion.EntityType;

    // Link definitions
    private static readonly LinkDefinition UserRole = new()
    {
        Left = User,
        Right = Role,
        Link = LinkTypeRegistry.MembershipRole
    };

    private static readonly LinkDefinition UserGroup = new()
    {
        Left = User,
        Right = Group,
        Link = LinkTypeRegistry.MembershipGroup
    };

    private static readonly LinkDefinition GroupRole = new()
    {
        Left = Group,
        Right = Role,
        Link = LinkTypeRegistry.GroupRole
    };

    // =========================================================================
    // Single-hop queries
    // =========================================================================

    [Fact]
    public async Task FindRolesAssignedToUserAsync()
    {
        // Given a user linked to a role, querying roles for that user returns the role.
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var userId = UuidV7.New();
        var roleId = UuidV7.New();

        _ = await store.CreateAsync(userId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(roleId, new RoleDso("admin"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(UserRole, userId, roleId, [], _ct);

        var query = LinkQuery.From(Role)
            .Join(UserRole)
            .Where(User, userId)
            .Build();

        var result = await queryStore.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("admin");
    }

    [Fact]
    public async Task FindUsersWithRoleAsync()
    {
        // Given a user linked to a role, querying users for that role returns the user (reverse traversal).
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var userId = UuidV7.New();
        var roleId = UuidV7.New();

        _ = await store.CreateAsync(userId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(roleId, new RoleDso("admin"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(UserRole, userId, roleId, [], _ct);

        var query = LinkQuery.From(User)
            .Join(UserRole)
            .Where(Role, roleId)
            .Build();

        var result = await queryStore.QueryLinksAsync<UserDso>(query, DataRange.FromPage(1, 100), _ct);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("alice");
    }

    [Fact]
    public async Task EmptyResultWhenNoRolesAssignedAsync()
    {
        // Querying roles for a user that has none returns an empty page.
        await using var fixture = await CreateProviderAsync();
        var queryStore = fixture.Store;

        var query = LinkQuery.From(Role)
            .Join(UserRole)
            .Where(User, UuidV7.New())
            .Build();

        var result = await queryStore.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    // =========================================================================
    // Pagination
    // =========================================================================

    [Fact]
    public async Task PaginateThroughGroupsForUserAsync()
    {
        // A user belongs to 5 groups; paging with size 3 yields two pages.
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var userId = UuidV7.New();
        _ = await store.CreateAsync(userId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);

        for (var i = 0; i < 5; i++)
        {
            var groupId = UuidV7.New();
            _ = await store.CreateAsync(groupId, new GroupDso($"group-{i}"), [], [], Expiration.NoExpiration, [], _ct);
            _ = await store.LinkAsync(UserGroup, userId, groupId, [], _ct);
        }

        var query = LinkQuery.From(Group)
            .Join(UserGroup)
            .Where(User, userId)
            .Build();

        var page1 = await queryStore.QueryLinksAsync<GroupDso>(query, DataRange.FromPage(1, 3), _ct);
        var page2 = await queryStore.QueryLinksAsync<GroupDso>(query, DataRange.FromPage(2, 3), _ct);

        page1.Items.Count.ShouldBe(3);
        page1.TotalCount.ShouldBe(5);

        page2.Items.Count.ShouldBe(2);
        page2.TotalCount.ShouldBe(5);
    }

    // =========================================================================
    // Distinct results
    // =========================================================================

    [Fact]
    public async Task RoleSharedByTwoUsersAppearsOnce()
    {
        // Two users share the same role — querying all roles should return it only once.
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var aliceId = UuidV7.New();
        var bobId = UuidV7.New();
        var roleId = UuidV7.New();

        _ = await store.CreateAsync(aliceId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(bobId, new UserDso("bob"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(roleId, new RoleDso("admin"), [], [], Expiration.NoExpiration, [], _ct);

        _ = await store.LinkAsync(UserRole, aliceId, roleId, [], _ct);
        _ = await store.LinkAsync(UserRole, bobId, roleId, [], _ct);

        var query = LinkQuery.From(Role)
            .Join(UserRole)
            .Build();

        var result = await queryStore.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);

        result.Items.Count(r => r.Value.Name == "admin").ShouldBe(1);
    }

    // =========================================================================
    // Multi-hop: User → Group → Role
    // =========================================================================

    [Fact]
    public async Task FindUsersWhoHaveRoleThroughGroupAsync()
    {
        // alice → engineers (UserGroup) → admin (GroupRole)
        // Multi-hop query: starting from User, traverse UserGroup then GroupRole,
        // filtered to a specific role — should return alice.
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var aliceId = UuidV7.New();
        var engineersId = UuidV7.New();
        var adminRoleId = UuidV7.New();

        _ = await store.CreateAsync(aliceId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(engineersId, new GroupDso("engineers"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(adminRoleId, new RoleDso("admin"), [], [], Expiration.NoExpiration, [], _ct);

        _ = await store.LinkAsync(UserGroup, aliceId, engineersId, [], _ct);
        _ = await store.LinkAsync(GroupRole, engineersId, adminRoleId, [], _ct);

        // From User, hop through UserGroup (User→Group), then GroupRole (Group→Role),
        // filter where Role = adminRoleId
        var query = LinkQuery.From(User)
            .Join(UserGroup)
            .Join(GroupRole)
            .Where(Role, adminRoleId)
            .Build();

        var result = await queryStore.QueryLinksAsync<UserDso>(query, DataRange.FromPage(1, 100), _ct);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("alice");
    }

    // =========================================================================
    // Multi-space isolation
    // =========================================================================

    [Fact]
    public async Task UserRoleLinkInSpaceANotVisibleInSpaceBAsync()
    {
        // Space A: alice → admin
        await using var fixtureA = await CreateProviderAsync();
        var storeA = fixtureA.Store;

        // Space B: separate provider = separate SpaceId
        await using var fixtureB = await CreateProviderAsync();
        var queryStoreB = fixtureB.Store;

        var userId = UuidV7.New();
        var roleId = UuidV7.New();

        _ = await storeA.CreateAsync(userId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeA.CreateAsync(roleId, new RoleDso("admin"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeA.LinkAsync(UserRole, userId, roleId, [], _ct);

        // Querying in space B should return nothing
        var query = LinkQuery.From(Role)
            .Join(UserRole)
            .Where(User, userId)
            .Build();

        var result = await queryStoreB.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SameUserRoleLinkInDifferentSpacesAreIndependentAsync()
    {
        await using var fixtureA = await CreateProviderAsync();
        var storeA = fixtureA.Store;
        var queryStoreA = fixtureA.Store;

        await using var fixtureB = await CreateProviderAsync();
        var storeB = fixtureB.Store;
        var queryStoreB = fixtureB.Store;

        var userId = UuidV7.New();
        var roleId = UuidV7.New();

        // Create the entities in both spaces
        _ = await storeA.CreateAsync(userId, new UserDso("user-a"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeA.CreateAsync(roleId, new RoleDso("role-a"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeB.CreateAsync(userId, new UserDso("user-b"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeB.CreateAsync(roleId, new RoleDso("role-b"), [], [], Expiration.NoExpiration, [], _ct);

        // Same link in both spaces
        _ = await storeA.LinkAsync(UserRole, userId, roleId, [], _ct);
        _ = await storeB.LinkAsync(UserRole, userId, roleId, [], _ct);

        var query = LinkQuery.From(Role)
            .Join(UserRole)
            .Where(User, userId)
            .Build();

        var resultA = await queryStoreA.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);
        var resultB = await queryStoreB.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);

        // Each space sees only its own link
        resultA.TotalCount.ShouldBe(1);
        resultA.Items.Count.ShouldBe(1);
        resultB.TotalCount.ShouldBe(1);
        resultB.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteInSpaceADoesNotAffectSpaceBAsync()
    {
        await using var fixtureA = await CreateProviderAsync();
        var storeA = fixtureA.Store;

        await using var fixtureB = await CreateProviderAsync();
        var storeB = fixtureB.Store;
        var queryStoreB = fixtureB.Store;

        var userId = UuidV7.New();
        var roleId = UuidV7.New();

        // Space B: create user and role, link them
        _ = await storeB.CreateAsync(userId, new UserDso("bob"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeB.CreateAsync(roleId, new RoleDso("editor"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeB.LinkAsync(UserRole, userId, roleId, [], _ct);

        // Space A: create and delete the same user id — should not affect space B
        _ = await storeA.CreateAsync(userId, new UserDso("alice"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storeA.DeleteAsync(User, userId, [], _ct);

        // Space B link still intact
        var query = LinkQuery.From(Role)
            .Join(UserRole)
            .Where(User, userId)
            .Build();

        var result = await queryStoreB.QueryLinksAsync<RoleDso>(query, DataRange.FromPage(1, 100), _ct);
        result.Items.Count.ShouldBe(1);
    }

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<UserDso>();
            services.AddDsoRegistration<RoleDso>();
            services.AddDsoRegistration<GroupDso>();
        });
}
