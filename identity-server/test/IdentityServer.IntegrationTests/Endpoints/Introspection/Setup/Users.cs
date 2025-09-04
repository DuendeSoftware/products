// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Introspection.Setup;

public static class Users
{
    public static List<TestUser> Get() => new List<TestUser>
        {
            new TestUser
            {
                SubjectId = "1",
                Username = "bob",
                Password = "bob",
                Claims = [
                    new Claim(JwtClaimTypes.Role, "Admin"),
                    new Claim(JwtClaimTypes.Role, "Geek"),
                    new Claim(JwtClaimTypes.Address, "{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }", IdentityServerConstants.ClaimValueTypes.Json)
                ]
            }
        };
}
