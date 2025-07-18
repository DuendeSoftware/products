// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Identity;

namespace Duende.IdentityServer.AspNetIdentity;

/// <summary>
/// Implements callback for SecurityStampValidator's OnRefreshingPrincipal event.
/// </summary>
public static class SecurityStampValidatorCallback
{
    /// <summary>
    /// Maintains the claims captured at login time that are not being created by ASP.NET Identity.
    /// This is needed to preserve claims such as idp, auth_time, amr.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public static Task UpdatePrincipal(SecurityStampRefreshingPrincipalContext context)
    {
        var newClaimTypes = context.NewPrincipal.Claims.Select(x => x.Type).ToArray();
        var currentClaimsToKeep = context.CurrentPrincipal.Claims.Where(x => !newClaimTypes.Contains(x.Type)).ToArray();

        var id = context.NewPrincipal.Identities.First();
        id.AddClaims(currentClaimsToKeep);

        return Task.CompletedTask;
    }
}
