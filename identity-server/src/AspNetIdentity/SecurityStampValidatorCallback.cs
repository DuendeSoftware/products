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
    /// <param name="sessionClaimsFilter">Instance of session claims filter used to filter the claims from the ClaimsPrincipal to
    /// those that are session claims which are not persisted by ASP.NET Identity and would otherwise bee lost when the principal
    /// is updated.</param>
    /// <returns></returns>
    public static async Task UpdatePrincipal(SecurityStampRefreshingPrincipalContext context, ISessionClaimsFilter sessionClaimsFilter)
    {
        if (context.NewPrincipal == null || !context.NewPrincipal.Identities.Any())
        {
            return;
        }

        var currentClaimsToKeep = await sessionClaimsFilter.FilterToSessionClaimsAsync(context);

        var id = context.NewPrincipal.Identities.First();
        id.AddClaims(currentClaimsToKeep);
    }
}
