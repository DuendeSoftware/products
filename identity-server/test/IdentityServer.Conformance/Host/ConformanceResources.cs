// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Hosts.Shared.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.Conformance.Host;

/// <summary>
/// Identity resources and test users for the conformance host. These extend the
/// shared dev host configuration with the "address" and "phone" scopes and the phone
/// claims. The OIDC Core scope tests (oidcc-scope-address, oidcc-scope-phone,
/// oidcc-scope-all) need them.
/// </summary>
internal static class ConformanceResources
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        .. TestResources.IdentityResources,
        new IdentityResources.Address(),
        new IdentityResources.Phone(),
    ];

    public static List<TestUser> Users =>
        TestUsers.Users
            .Select(user =>
            {
                user.Claims.Add(new Claim(JwtClaimTypes.PhoneNumber, "+49 6221 123456"));
                user.Claims.Add(new Claim(JwtClaimTypes.PhoneNumberVerified, "true", ClaimValueTypes.Boolean));
                return user;
            })
            .ToList();
}
