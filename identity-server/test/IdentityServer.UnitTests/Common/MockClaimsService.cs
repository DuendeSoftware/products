// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;

namespace UnitTests.Common;

internal class MockClaimsService : IClaimsService
{
    public List<Claim> IdentityTokenClaims { get; set; } = new List<Claim>();
    public List<Claim> AccessTokenClaims { get; set; } = new List<Claim>();

    public Task<IReadOnlyCollection<Claim>> GetIdentityTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resources, bool includeAllIdentityClaims, ValidatedRequest request, Ct _) => Task.FromResult<IReadOnlyCollection<Claim>>(IdentityTokenClaims);

    public Task<IReadOnlyCollection<Claim>> GetAccessTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resources, ValidatedRequest request, Ct _) => Task.FromResult<IReadOnlyCollection<Claim>>(AccessTokenClaims);
}
