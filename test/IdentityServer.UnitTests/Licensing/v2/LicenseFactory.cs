// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Duende.IdentityServer.Licensing.v2;

namespace IdentityServer.UnitTests.Licensing.v2;

internal static class LicenseFactory
{
    public static License Create(LicenseEdition edition, DateTimeOffset? expiration = null, bool redistribution = false)
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
        return new(new ClaimsPrincipal(new ClaimsIdentity(claims)));
    }
}