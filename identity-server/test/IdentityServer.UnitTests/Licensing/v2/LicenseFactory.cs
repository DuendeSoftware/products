// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende;
using Duende.IdentityServer;
using Duende.IdentityServer.Licensing.V2;

namespace IdentityServer.UnitTests.Licensing.V2;

internal static class LicenseFactory
{
    public static License Create(License.LicenseEdition edition, DateTimeOffset? expiration = null, bool redistribution = false)
    {
        expiration ??= DateTimeOffset.MaxValue;
        var claims = new List<Claim>
        {
            new Claim("exp", expiration.Value.ToUnixTimeSeconds().ToString()),
            new Claim("edition", edition.ToString()),
        };
        if (redistribution)
        {
            claims.Add(new Claim("feature", "redistribution"));
        }
        return new IdentityServerLicense(claims.ToArray());
    }
}
