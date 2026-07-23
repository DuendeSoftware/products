// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Duende.UserManagement.Scim.Internal;

internal sealed class ScimScopeRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> RequiredScopes { get; }

    public ScimScopeRequirement(params string[] requiredScopes) => RequiredScopes = requiredScopes;
}
