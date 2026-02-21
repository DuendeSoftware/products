// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Duende.IdentityServer.AspNetIdentity;

public class DefaultSessionClaimsFilter : ISessionClaimsFilter
{
    /// <inheritdoc/>
    public Task<IReadOnlyCollection<Claim>> FilterToSessionClaimsAsync(SecurityStampRefreshingPrincipalContext context, CT ct)
    {
        var newClaimTypes = context.NewPrincipal.Claims.Select(x => x.Type).ToArray();
        var currentClaimsToKeep = context.CurrentPrincipal.Claims.Where(x => !newClaimTypes.Contains(x.Type)).ToArray();

        var id = context.NewPrincipal.Identities.First();
        id.AddClaims(currentClaimsToKeep);

        return Task.FromResult<IReadOnlyCollection<Claim>>(currentClaimsToKeep);
    }
}
