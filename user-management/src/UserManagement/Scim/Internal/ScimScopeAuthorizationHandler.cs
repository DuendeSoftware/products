// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Authorization handler that validates the authenticated user has one of the required SCIM scopes.
/// Handles both individual scope claims (array format) and space-delimited scope claims (single string).
/// </summary>
internal sealed class ScimScopeAuthorizationHandler : AuthorizationHandler<ScimScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScimScopeRequirement requirement)
    {
        var scopeClaims = context.User.FindAll("scope");

        var userScopes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in scopeClaims)
        {
            // Handle space-delimited format (single claim with multiple scopes)
            if (claim.Value.Contains(' ', StringComparison.Ordinal))
            {
                foreach (var scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    _ = userScopes.Add(scope);
                }
            }
            else
            {
                // Handle individual claim format (one claim per scope)
                _ = userScopes.Add(claim.Value);
            }
        }

        foreach (var requiredScope in requirement.RequiredScopes)
        {
            if (userScopes.Contains(requiredScope))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
