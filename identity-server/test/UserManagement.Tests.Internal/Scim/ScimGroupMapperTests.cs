// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Groups;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimGroupMapperTests
{
    private const string BaseUrl = "https://example.com";
    private const string GroupsRoute = "/scim/Groups";
    private const string UsersRoute = "/scim/Users";

    [Fact]
    public void MapToResourceWithVersionCreatesCorrectResource()
    {
        var groupId = GroupId.New();
        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = GroupName.Create("Engineers")
        };

        var resource = ScimGroupMapper.MapToResource(groupListDto, null, 1, BaseUrl, GroupsRoute);

        resource.Id.ShouldBe(groupId.Value.ToString());
        resource.DisplayName.ShouldBe("Engineers");
        resource.Schemas.ShouldContain("urn:ietf:params:scim:schemas:core:2.0:Group");
        resource.Members.ShouldBeNull();
        resource.Meta.ResourceType.ShouldBe("Group");
        resource.Meta.Location.ShouldBe($"{BaseUrl}{GroupsRoute}/{groupId.Value}");
        resource.Meta.Version.ShouldBe("W/\"1\"");
    }

    [Fact]
    public void MapToResourceForListCreatesResourceWithoutVersionOrMembers()
    {
        var groupId = GroupId.New();
        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = GroupName.Create("Marketing")
        };

        var resource = ScimGroupMapper.MapToResource(groupListDto, null, BaseUrl, GroupsRoute);

        resource.Id.ShouldBe(groupId.Value.ToString());
        resource.DisplayName.ShouldBe("Marketing");
        resource.Members.ShouldBeNull();
        resource.Meta.Version.ShouldBeNull();
    }

    [Fact]
    public void MapToResourceWithMembersIncludesMemberArray()
    {
        var groupId = GroupId.New();
        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = GroupName.Create("WithMembers")
        };

        var members = new List<ScimGroupMember>
        {
            new()
            {
                Value = "user-1",
                Ref = $"{BaseUrl}{UsersRoute}/user-1",
                Type = "User"
            }
        };

        var resource = ScimGroupMapper.MapToResource(groupListDto, members, 2, BaseUrl, GroupsRoute);

        var actualMembers = resource.Members.ShouldNotBeNull();
        actualMembers.Count.ShouldBe(1);
        actualMembers[0].Value.ShouldBe("user-1");
        actualMembers[0].Type.ShouldBe("User");
        actualMembers[0].Ref.ShouldBe($"{BaseUrl}{UsersRoute}/user-1");
    }

    [Fact]
    public void MapToMemberCreatesCorrectMember()
    {
        var subjectId = UserSubjectId.New();
        var memberDto = new MembershipGroupMemberListItem
        {
            SubjectId = subjectId
        };

        var member = ScimGroupMapper.MapToMember(memberDto, BaseUrl, UsersRoute);

        var expectedId = subjectId.ToString();
        member.Value.ShouldBe(expectedId);
        member.Type.ShouldBe("User");
        member.Ref.ShouldBe($"{BaseUrl}{UsersRoute}/{expectedId}");
    }

    [Fact]
    public void MapToResourceWithEmptyMembersListSetsNull()
    {
        var groupId = GroupId.New();
        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = GroupName.Create("Empty")
        };

        var resource = ScimGroupMapper.MapToResource(
            groupListDto, null, 1, BaseUrl, GroupsRoute);

        resource.Members.ShouldBeNull();
    }
}
