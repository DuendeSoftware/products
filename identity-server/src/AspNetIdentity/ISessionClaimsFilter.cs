// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Duende.IdentityServer.AspNetIdentity;

public interface ISessionClaimsFilter
{
    /// <summary>
    /// Filters the claims in the given SecurityStampRefreshingPrincipalContext to those that should be kept for the session.
    /// These claims are not claims persisted by ASP.NET Identity, but are typically captured and login time and need to be
    /// persisted across updates to the ClaimsPrincipal in the <see cref="SecurityStampValidatorOptions.OnRefreshingPrincipal"/>
    /// method.
    /// </summary>
    /// <param name="context">The SecurityStampRefreshingPrincipalContext <see cref="SecurityStampRefreshingPrincipalContext.SecurityStampRefreshingPrincipalContext"/>
    /// in the call to <see cref="SecurityStampValidatorOptions.OnRefreshingPrincipal"/>.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The claims of the ClaimsPrincipal which should be persisted for the session.</returns>
    public Task<IReadOnlyCollection<Claim>> FilterToSessionClaimsAsync(SecurityStampRefreshingPrincipalContext context, CT ct);
}
