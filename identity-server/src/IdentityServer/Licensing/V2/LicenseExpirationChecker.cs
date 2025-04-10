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
    private readonly ILogger _logger = loggerFactory.CreateLogger("Duende.IdentityServer.License");

    private bool _expiredLicenseWarned;

    public void CheckExpiration()
    {
        if (!_expiredLicenseWarned && !license.Current.Redistribution && IsExpired)
        {
            _expiredLicenseWarned = true;
            _logger.LicenseHasExpired();
        }
    }

    private bool IsExpired => clock.UtcNow > license.Current.Expiration;
}
