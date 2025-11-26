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

    private bool? _licenseCheckResult;
    internal const int MaximumAllowedSessionsInTrialMode = 5;

    public bool CheckLicense()
    {
        if (_licenseCheckResult != null)
        {
            return _licenseCheckResult.Value;
        }

        _licenseCheckResult = CheckLicenseValidity();
        return _licenseCheckResult.Value;
    }

    private bool CheckLicenseValidity()
    {
        if (!license.IsConfigured)
        {
            logger.NoValidLicense(LogLevel.Error);
            return false;
        }

        //An expired license is still considered a valid license.
        if (license.Expiration <= timeProvider.GetUtcNow())
        {
            logger.LicenseHasExpired(LogLevel.Warning, license.Expiration, license.ContactInfo,
                license.CompanyName);
        }

        logger.LicenseDetails(
            LogLevel.Debug,
            license.Expiration,
            license.ContactInfo,
            license.CompanyName);

        return true;
    }
}
