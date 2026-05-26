// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;

namespace Duende.UserManagement.Authentication.Internal;

internal sealed class UserAuthenticationFeature : IDuendePlatformFeature
{
    public string Name => "UserAuthentication";
}
