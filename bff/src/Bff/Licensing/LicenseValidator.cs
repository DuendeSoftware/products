// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licensing;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Licensing;

internal class LicenseValidator(ILogger<LicenseValidator> logger, LicenseAccessor<BffLicense> license, TimeProvider timeProvider)
{
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

        if (!license.Current.IsConfigured)
        {
            logger.NoValidLicense(LogLevel.Error);
            return false;
        }

        if (license.Current.Expiration <= timeProvider.GetUtcNow())
        {
            logger.LicenseHasExpired(LogLevel.Error, license.Current.Expiration, license.Current.ContactInfo, license.Current.CompanyName);
            return false;
        }

        if (!license.Current.BffFeature)
        {
            logger.NotLicensedForBff(LogLevel.Error, license.Current.ContactInfo, license.Current.CompanyName);
            return false;
        }

        logger.LicenseDetails(
            LogLevel.Debug,
            license.Current.Edition.ToString(),
            license.Current.Expiration,
            license.Current.ContactInfo,
            license.Current.CompanyName,
            license.Current.FrontendLimit switch
            {
                null => "not licensed for multi-frontend feature",
                0 => "not licensed for multi-frontend feature",
                -1 => "unlimited",
                > 0 => license.Current.FrontendLimit.ToString(),
                // Should't happen, but just in case
                _ => "not licensed for multi-frontend feature"
            });

        return true;
    }
}
