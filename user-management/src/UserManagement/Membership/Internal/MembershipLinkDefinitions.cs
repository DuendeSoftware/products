// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership.Internal.Storage;

namespace Duende.UserManagement.Membership.Internal;

internal static class MembershipLinkDefinitions
{
    internal static readonly LinkDefinition MembershipRole = new()
    {
        Left = UserDso.EntityType,
        Right = RoleDso.EntityType,
        Link = LinkTypeRegistry.MembershipRole
    };

    internal static readonly LinkDefinition MembershipGroup = new()
    {
        Left = UserDso.EntityType,
        Right = GroupDso.EntityType,
        Link = LinkTypeRegistry.MembershipGroup
    };

    internal static readonly LinkDefinition GroupRole = new()
    {
        Left = GroupDso.EntityType,
        Right = RoleDso.EntityType,
        Link = LinkTypeRegistry.GroupRole
    };
}
