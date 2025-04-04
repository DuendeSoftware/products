// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseExpirationChecker(
    LicenseAccessor license,
    IClock clock,
    ILoggerFactory loggerFactory)
{
    private readonly LicenseLoggingAdapter _logger = new(license.Current.Redistribution, loggerFactory.CreateLogger("Duende.IdentityServer.License"));

    private bool _expiredLicenseWarned;

    public void CheckExpiration()
    {
        if (!_expiredLicenseWarned && IsExpired)
        {
            _expiredLicenseWarned = true;
            _logger.LogError("The IdentityServer license is expired. In a future version of IdentityServer, license expiration will be enforced after a grace period.");
        }
    }

    private bool IsExpired => clock.UtcNow > license.Current.Expiration;
}
