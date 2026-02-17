// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseExpirationChecker(
    LicenseAccessor license,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Duende.IdentityServer.License");

    private bool _expiredLicenseWarned;

    public void CheckExpiration()
    {
        if (!_expiredLicenseWarned && !license.Current.Redistribution && IsExpired)
        {
            _expiredLicenseWarned = true;
            _logger.LicenseHasExpired(license.Current.ContactInfo ?? "<contact info missing>", license.Current.CompanyName ?? "<company name missing>");
        }
    }

    private bool IsExpired => timeProvider.GetUtcNow() > license.Current.Expiration;
}
