// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Registers SCIM authorization policies for read and write access.
/// </summary>
internal sealed class ScimAuthorizationPolicyProvider
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<IAuthorizationHandler, ScimScopeAuthorizationHandler>();

        _ = services.AddAuthorizationBuilder()
            .AddPolicy(ScimConstants.WritePolicyName, policy => policy
                .AddAuthenticationSchemes(ScimConstants.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new ScimScopeRequirement("scim")))
            .AddPolicy(ScimConstants.ReadPolicyName, policy => policy
                .AddAuthenticationSchemes(ScimConstants.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new ScimScopeRequirement("scim", "scim.read")));
    }
}
