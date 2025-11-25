// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Licensing;

internal class LicenseValidator(ILogger<LicenseValidator> logger, License license, TimeProvider timeProvider)
{
    internal LicenseValidator(ILogger<LicenseValidator> logger, ClaimsPrincipal claims, TimeProvider timeProvider)
        : this(logger, new License(claims), timeProvider)
    {
    }

    public void CheckLicenseValidity()
    {
        if (!license.IsConfigured)
        {
            logger.NoValidLicense(LogLevel.Error);
        }

        if (license.Expiration <= timeProvider.GetUtcNow())
        {
            logger.LicenseHasExpired(LogLevel.Error, license.Expiration, license.ContactInfo ?? "", license.CompanyName ?? "");
        }
    }
}
