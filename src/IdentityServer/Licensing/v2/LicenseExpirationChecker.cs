// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.v2;

internal class LicenseExpirationChecker : ILicenseExpirationChecker
{
    private readonly ILicenseAccessor _license;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

        public LicenseExpirationChecker(
        ILicenseAccessor license,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _license = license;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger("Duende.IdentityServer.License");
    }

    /// <summary>
    /// The amount of time an expired license may be used for before we shut down the server.
    /// </summary>
    private readonly TimeSpan _gracePeriod = TimeSpan.FromDays(90);

    private bool _expiredLicenseWarned;

    public void CheckExpiration()
    {
        if (!_expiredLicenseWarned && !_license.Current.Redistribution && IsExpired) 
        {
            _expiredLicenseWarned = true;
            _logger.LogError("The IdentityServer license is expired. In a future version of IdentityServer, expired licenses will stop the server after {gracePeriod} days.", _gracePeriod.Days);
        }
    }

    private bool IsExpired => _timeProvider.GetUtcNow() > _license.Current.Expiration;
}
