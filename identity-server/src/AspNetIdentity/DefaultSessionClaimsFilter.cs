// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;

namespace Duende.IdentityServer.AspNetIdentity;

public class DefaultSessionClaimsFilter : ISessionClaimsFilter
{
    private static readonly List<string> ClaimTypesToKeep = [JwtClaimTypes.AuthenticationMethod, JwtClaimTypes.IdentityProvider, JwtClaimTypes.AuthenticationTime];

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<Claim>> FilterToSessionClaimsAsync(ClaimsPrincipal principal) => Task.FromResult<IReadOnlyCollection<Claim>>(principal.Claims.Where(claim => ClaimTypesToKeep.Contains(claim.Type)).ToList());
}
