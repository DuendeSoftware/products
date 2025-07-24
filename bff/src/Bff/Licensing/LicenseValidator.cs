// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using Duende.Bff.DynamicFrontends;
using Duende.Private.Licensing;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Licensing;

internal class LicenseValidator(ILogger<LicenseValidator> logger, BffLicense license, TimeProvider timeProvider)
{
    internal LicenseValidator(ILogger<LicenseValidator> logger, ClaimsPrincipal claims, TimeProvider timeProvider)
    : this(logger, new BffLicense(claims), timeProvider)
    {

    }

    private bool? _licenseCheckResult;

    public bool IsValid()
    {
        if (_licenseCheckResult != null)
        {
            return _licenseCheckResult.Value;
        }
        _licenseCheckResult = CheckLicense();
        return _licenseCheckResult.Value;

    }

    private bool CheckLicense()
    {

        if (!license.IsConfigured)
        {
            logger.NoValidLicense(LogLevel.Error);
            return false;
        }

        if (license.Expiration <= timeProvider.GetUtcNow())
        {
            logger.LicenseHasExpired(LogLevel.Error, license.Expiration, license.ContactInfo, license.CompanyName);
            return false;
        }

        if (!license.BffFeature)
        {
            logger.NotLicensedForBff(LogLevel.Error, license.ContactInfo, license.CompanyName);
            return false;
        }

        logger.LicenseDetails(
            LogLevel.Debug,
            license.Edition.ToString(),
            license.Expiration,
            license.ContactInfo,
            license.CompanyName,
            license.FrontendLimit switch
            {
                null => "not licensed for multi-frontend feature",
                0 => "not licensed for multi-frontend feature",
                -1 => "unlimited",
                > 0 => license.FrontendLimit.Value.ToString(CultureInfo.InvariantCulture),
                // Should't happen, but just in case
                _ => "not licensed for multi-frontend feature"
            });

        return true;
    }

    public void LogFrontendAdded(BffFrontendName frontendName, int frontendCount)
    {
        if (license?.FrontendLimit == null)
        {
            logger.NotLicensedForMultiFrontend(LogLevel.Error, frontendName);
            return;
        }
        if (license.FrontendLimit == -1)
        {
            // unlimited frontends
            logger.UnlimitedFrontends(LogLevel.Debug, frontendName, frontendCount);
            return;
        }

        if (license.FrontendLimit < frontendCount)
        {
            logger.FrontendLimitExceeded(LogLevel.Error, frontendName, frontendCount, license.FrontendLimit.Value);
            return;
        }

        logger.FrontendAdded(LogLevel.Debug, frontendName, frontendCount, license.FrontendLimit.Value);
    }
}
