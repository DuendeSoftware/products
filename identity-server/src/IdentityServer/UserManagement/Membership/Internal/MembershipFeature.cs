// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.UserManagement.Internal.Modules;

namespace Duende.UserManagement.Membership.Internal;

internal sealed class MembershipFeature : IDuendePlatformFeature
{
    public string Name => "Membership";
}
